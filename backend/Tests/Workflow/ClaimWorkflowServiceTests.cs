using FluentValidation;
using Mems.Application.Auth;
using Mems.Application.Claims;
using Mems.Application.Entitlement;
using Mems.Application.Notifications;
using Mems.Application.Receipts;
using Mems.Application.Workflow;
using Mems.Domain.Claims;
using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Tests.Workflow;

public sealed class ClaimWorkflowServiceTests
{
    private static readonly AuthUser Employee = new("ayesha@example.test", "Ayesha", UserRole.Employee, "DEMO-M-002");
    private static readonly AuthUser ExecEmployee = new("khalid@example.test", "Khalid", UserRole.Employee, "DEMO-E-001");
    private static readonly AuthUser LineManager = new("manager@example.test", "Bilal", UserRole.LineManager, null);
    private static readonly AuthUser AdminHr = new("hr@example.test", "Sana", UserRole.AdminHr, null);
    private static readonly AuthUser TopLevel = new("ed@example.test", "Director", UserRole.TopLevel, null);
    // The routed finance identities use the DEFAULT ApprovalOptions emails.
    private static readonly AuthUser FinanceMGrade = new("mapproval@waves.com.pk", "Finance (M-Grade)", UserRole.Finance, null);
    private static readonly AuthUser FinanceOther = new("otherapproval@waves.com.pk", "Finance (Corporate)", UserRole.Finance, null);

    private readonly FakeClaimRepository _claims = new();
    private readonly FakeReceiptRepository _receipts = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly ApprovalOptions _options = new();
    private readonly ClaimWorkflowService _service;

    public ClaimWorkflowServiceTests()
    {
        _service = new ClaimWorkflowService(
            _claims, _receipts, new SubmitClaimRequestValidator(), _options,
            new FinanceRoutingService(new FakeProfileProvider(), _options), _notifier);
    }

    // ---- Feature B: the Line Manager stage cannot reduce ----

    [Fact]
    public async Task Line_manager_cannot_reduce()
    {
        var claim = SeedClaim(ApprovalStage.LineManager, 1_000m);
        var lineId = claim.Lines[0].Id;

        await Assert.ThrowsAsync<WorkflowForbiddenException>(() =>
            _service.DecideAsync(claim.Id, LineManager, new[] { new LineDecisionInput(lineId, "reduce", 400m, null) }));

        // Nothing mutated: the claim is still at Line Manager with the line untouched.
        Assert.Equal(ApprovalStage.LineManager, claim.Stage);
        Assert.Equal(LineDecisionStatus.Pending, claim.Lines[0].Status);
        Assert.Null(claim.Lines[0].ApprovedAmount);
    }

    [Fact]
    public async Task Line_manager_can_approve_in_full()
    {
        var claim = SeedClaim(ApprovalStage.LineManager, 1_000m);

        await _service.DecideAsync(claim.Id, LineManager, new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) });

        Assert.Equal(ApprovalStage.AdminHr, claim.Stage);
    }

    [Fact]
    public async Task Line_manager_can_reject_with_reason()
    {
        var claim = SeedClaim(ApprovalStage.LineManager, 1_000m);

        await _service.DecideAsync(claim.Id, LineManager, new[] { new LineDecisionInput(claim.Lines[0].Id, "reject", null, "No receipt visible") });

        Assert.Equal(ApprovalStage.ReturnedToEmployee, claim.Stage);
    }

    [Fact]
    public async Task Admin_hr_can_still_reduce()
    {
        var claim = SeedClaim(ApprovalStage.AdminHr, 1_000m);

        await _service.DecideAsync(claim.Id, AdminHr,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "reduce", 400m, null, "Exceeds the room-rate cap") });

        // The reduced amount carries forward for the next stage (Finance) to see.
        Assert.Equal(ApprovalStage.Finance, claim.Stage);
        Assert.Equal(400m, claim.Lines[0].CurrentAmount);
    }

    [Fact]
    public async Task Reducing_without_a_comment_is_rejected()
    {
        var claim = SeedClaim(ApprovalStage.AdminHr, 1_000m);

        await Assert.ThrowsAsync<WorkflowConflictException>(() => _service.DecideAsync(claim.Id, AdminHr,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "reduce", 400m, null) }));
    }

    // ---- Top-Level adjust (up to the original claim) and finality ----

    [Fact]
    public async Task Top_level_can_adjust_back_up_to_the_claimed_amount()
    {
        var claim = SeedClaim(ApprovalStage.TopLevel, 1_000m);
        claim.Lines[0].CurrentAmount = 400m; // reduced at an earlier stage

        await _service.DecideAsync(claim.Id, TopLevel,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "reduce", 1_000m, null, "Restoring the full claim") });

        Assert.Equal(LineDecisionStatus.Approved, claim.Lines[0].Status); // full claim → Approved
        Assert.Equal(1_000m, claim.Lines[0].ApprovedAmount);
        Assert.Equal(ApprovalStage.Completed, claim.Stage);
    }

    [Fact]
    public async Task Top_level_cannot_adjust_above_the_claimed_amount()
    {
        var claim = SeedClaim(ApprovalStage.TopLevel, 1_000m);

        await Assert.ThrowsAsync<WorkflowConflictException>(() => _service.DecideAsync(claim.Id, TopLevel,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "reduce", 1_001m, null, "too much") }));
    }

    [Fact]
    public async Task Top_level_adjust_below_the_claim_is_a_reduction()
    {
        var claim = SeedClaim(ApprovalStage.TopLevel, 1_000m);

        await _service.DecideAsync(claim.Id, TopLevel,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "reduce", 700m, null, "partial coverage") });

        Assert.Equal(LineDecisionStatus.Reduced, claim.Lines[0].Status);
        Assert.Equal(700m, claim.Lines[0].ApprovedAmount);
    }

    [Fact]
    public async Task Top_level_mixed_decision_is_final_and_completes()
    {
        var claim = SeedTwoLineClaim();
        claim.Stage = ApprovalStage.TopLevel;

        await _service.DecideAsync(claim.Id, TopLevel, new[]
        {
            new LineDecisionInput(claim.Lines[0].Id, "approve", null, null),
            new LineDecisionInput(claim.Lines[1].Id, "reject", null, "Not covered by policy"),
        });

        Assert.Equal(ApprovalStage.Completed, claim.Stage); // never returns to the employee
        Assert.Equal(LineDecisionStatus.Rejected, claim.Lines[1].Status);
        Assert.False(ClaimMapper.Editable(claim));
        Assert.Equal(ClaimOverallStatus.PartiallyApproved, ClaimMapper.OverallStatus(claim));
        Assert.Contains(claim.Id, _notifier.Completed);
        Assert.DoesNotContain(claim.Id, _notifier.Returned);
    }

    [Fact]
    public async Task Top_level_rejecting_everything_closes_the_claim()
    {
        var claim = SeedTwoLineClaim();
        claim.Stage = ApprovalStage.TopLevel;

        await _service.DecideAsync(claim.Id, TopLevel, new[]
        {
            new LineDecisionInput(claim.Lines[0].Id, "reject", null, "Not covered"),
            new LineDecisionInput(claim.Lines[1].Id, "reject", null, "Not covered"),
        });

        Assert.Equal(ApprovalStage.Closed, claim.Stage);
        Assert.Equal(ClaimOverallStatus.Rejected, ClaimMapper.OverallStatus(claim));
        Assert.False(ClaimMapper.Editable(claim));
        Assert.Empty(await _claims.GetPostableAsync());
        Assert.Contains(claim.Id, _notifier.Closed);
    }

    [Fact]
    public async Task Top_level_reject_all_still_completes_when_a_locked_line_was_approved_earlier()
    {
        var claim = SeedTwoLineClaim();
        claim.Stage = ApprovalStage.TopLevel;
        // Line0 was approved (locked) in an earlier round; only line1 is pending at ED.
        claim.Lines[0].Status = LineDecisionStatus.Approved;
        claim.Lines[0].ApprovedAmount = 1_000m;

        await _service.DecideAsync(claim.Id, TopLevel,
            new[] { new LineDecisionInput(claim.Lines[1].Id, "reject", null, "Not covered") });

        // The locked approved amount must still be posted.
        Assert.Equal(ApprovalStage.Completed, claim.Stage);
    }

    [Fact]
    public void Approval_stage_policy_only_blocks_reduce_for_line_manager()
    {
        Assert.False(ApprovalStagePolicy.CanReduce(ApprovalStage.LineManager));
        Assert.True(ApprovalStagePolicy.CanReduce(ApprovalStage.AdminHr));
        Assert.True(ApprovalStagePolicy.CanReduce(ApprovalStage.Finance));
        Assert.True(ApprovalStagePolicy.CanReduce(ApprovalStage.TopLevel));
    }

    // ---- Receipt integrity at submit ----

    [Fact]
    public async Task Submitting_with_unknown_receipt_id_fails()
    {
        var request = Request("DEMO-M-002", Guid.NewGuid()); // id never uploaded

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(Employee, request));
    }

    [Fact]
    public async Task Submitting_with_another_employees_receipt_id_fails()
    {
        var foreign = _receipts.Seed("SOMEONE-ELSE");
        var request = Request("DEMO-M-002", foreign);

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(Employee, request));
    }

    [Fact]
    public async Task Submitting_with_an_owned_receipt_id_succeeds()
    {
        var owned = _receipts.Seed("DEMO-M-002");
        var request = Request("DEMO-M-002", owned);

        var claim = await _service.SubmitAsync(Employee, request);

        Assert.Equal(owned, claim.Lines[0].ReceiptIds.Single());
        Assert.Equal(ApprovalStage.LineManager, claim.Stage);
    }

    // ---- Line events + notifier emission ----

    [Fact]
    public async Task Submit_writes_a_submitted_event_per_line_with_the_comment_and_notifies_the_line_manager()
    {
        var owned = _receipts.Seed("DEMO-M-002");
        var claim = await _service.SubmitAsync(Employee, Request("DEMO-M-002", owned, comment: "follow-up visit"));

        var evt = Assert.Single(claim.LineEvents);
        Assert.Equal("Submitted", evt.Action);
        Assert.Equal(claim.Lines[0].Id, evt.LineId);
        Assert.Equal("follow-up visit", evt.Comment);
        Assert.Contains((claim.Id, ApprovalStage.LineManager), _notifier.Awaiting);
    }

    [Fact]
    public async Task Decisions_write_line_events_with_amounts_reasons_and_comments()
    {
        var claim = SeedTwoLineClaim();
        var (a, b) = (claim.Lines[0].Id, claim.Lines[1].Id);

        await _service.DecideAsync(claim.Id, LineManager, new[]
        {
            new LineDecisionInput(a, "approve", null, null, "receipt is clear"),
            new LineDecisionInput(b, "reject", null, "No receipt visible", null),
        });

        var approvedEvt = Assert.Single(claim.LineEvents, e => e.LineId == a);
        Assert.Equal("Approved", approvedEvt.Action);
        Assert.Equal(1_000m, approvedEvt.Amount);
        Assert.Equal("receipt is clear", approvedEvt.Comment);

        var rejectedEvt = Assert.Single(claim.LineEvents, e => e.LineId == b);
        Assert.Equal("Rejected", rejectedEvt.Action);
        Assert.Equal("No receipt visible", rejectedEvt.Reason);
        Assert.Contains(claim.Id, _notifier.Returned);
    }

    // ---- Partial resubmit of a returned claim (approved lines locked) ----

    [Fact]
    public async Task Resubmit_locks_approved_lines_and_reopens_only_the_rejected_one()
    {
        var claim = await ReturnedTwoLineClaim(); // line0 Approved(1000), line1 Rejected
        var locked = claim.Lines[0];
        var rejected = claim.Lines[1];
        var newReceipt = _receipts.Seed("DEMO-M-002");

        await _service.EditAsync(claim.Id, Employee, EditRequest(claim,
            LineFrom(locked),
            LineFrom(rejected, newAmount: 1_500m, newReceiptId: newReceipt, comment: "re-checked the bill")));

        Assert.Equal(ApprovalStage.LineManager, claim.Stage);
        Assert.Equal(LineDecisionStatus.Approved, locked.Status);
        Assert.Equal(1_000m, locked.ApprovedAmount);
        Assert.Equal(1_000m, locked.CurrentAmount); // normalized to the decided amount
        Assert.Equal(LineDecisionStatus.Pending, rejected.Status);
        Assert.Equal(1_500m, rejected.ClaimedAmount);
        Assert.Null(rejected.RejectionReason);
        var resubmitted = Assert.Single(claim.LineEvents, e => e.Action == "Resubmitted");
        Assert.Equal(rejected.Id, resubmitted.LineId);
        Assert.Equal("re-checked the bill", resubmitted.Comment);
    }

    [Fact]
    public async Task Locked_line_receipt_sets_compare_order_insensitively()
    {
        var claim = SeedTwoLineClaim();
        var (id1, id2) = (_receipts.Seed("DEMO-M-002"), _receipts.Seed("DEMO-M-002"));
        claim.Lines[0].ReceiptIds = new List<Guid> { id1, id2 };
        await _service.DecideAsync(claim.Id, LineManager, new[]
        {
            new LineDecisionInput(claim.Lines[0].Id, "approve", null, null),
            new LineDecisionInput(claim.Lines[1].Id, "reject", null, "Blurry receipt"),
        });

        // Same ids, reversed order → accepted; a different set → 409.
        var reorderedLocked = LineFrom(claim.Lines[0]) with { ReceiptIds = new[] { id2, id1 } };
        var editable = LineFrom(claim.Lines[1], newReceiptId: _receipts.Seed("DEMO-M-002"));
        await _service.EditAsync(claim.Id, Employee, EditRequest(claim, reorderedLocked, editable));
        Assert.Equal(ApprovalStage.LineManager, claim.Stage);

        await _service.DecideAsync(claim.Id, LineManager, new[]
        {
            new LineDecisionInput(claim.Lines[1].Id, "reject", null, "still blurry"),
        });
        var tamperedLocked = LineFrom(claim.Lines[0]) with { ReceiptIds = new[] { id1 } };
        await Assert.ThrowsAsync<WorkflowConflictException>(() => _service.EditAsync(claim.Id, Employee,
            EditRequest(claim, tamperedLocked, LineFrom(claim.Lines[1], newReceiptId: _receipts.Seed("DEMO-M-002")))));
    }

    [Fact]
    public async Task Resubmit_rejects_changes_to_a_locked_line()
    {
        var claim = await ReturnedTwoLineClaim();
        var newReceipt = _receipts.Seed("DEMO-M-002");

        await Assert.ThrowsAsync<WorkflowConflictException>(() =>
            _service.EditAsync(claim.Id, Employee, EditRequest(claim,
                LineFrom(claim.Lines[0], newAmount: 999m), // locked line tampered
                LineFrom(claim.Lines[1], newReceiptId: newReceipt))));
    }

    [Fact]
    public async Task Resubmit_rejects_added_or_removed_lines()
    {
        var claim = await ReturnedTwoLineClaim();
        var newReceipt = _receipts.Seed("DEMO-M-002");

        await Assert.ThrowsAsync<WorkflowConflictException>(() =>
            _service.EditAsync(claim.Id, Employee, EditRequest(claim,
                LineFrom(claim.Lines[1], newReceiptId: newReceipt)))); // dropped the locked line
    }

    [Fact]
    public async Task Locked_lines_are_never_redecided_and_their_amounts_count_toward_completion()
    {
        var claim = await ReturnedTwoLineClaim();
        var lockedId = claim.Lines[0].Id;
        var pendingId = claim.Lines[1].Id;
        var newReceipt = _receipts.Seed("DEMO-M-002");
        await _service.EditAsync(claim.Id, Employee, EditRequest(claim,
            LineFrom(claim.Lines[0]),
            LineFrom(claim.Lines[1], newReceiptId: newReceipt)));

        // A decision aimed at the locked line is refused outright.
        await Assert.ThrowsAsync<WorkflowConflictException>(() =>
            _service.DecideAsync(claim.Id, LineManager, new[]
            {
                new LineDecisionInput(lockedId, "approve", null, null),
                new LineDecisionInput(pendingId, "approve", null, null),
            }));

        // The chain runs on the pending line only.
        await _service.DecideAsync(claim.Id, LineManager, new[] { new LineDecisionInput(pendingId, "approve", null, null) });
        await _service.DecideAsync(claim.Id, AdminHr, new[] { new LineDecisionInput(pendingId, "approve", null, null) });
        await _service.DecideAsync(claim.Id, FinanceMGrade, new[] { new LineDecisionInput(pendingId, "approve", null, null) });

        Assert.Equal(ApprovalStage.Completed, claim.Stage);
        Assert.Equal(1_000m, claim.Lines.First(l => l.Id == lockedId).ApprovedAmount); // untouched
        Assert.Equal(2_000m, claim.Lines.First(l => l.Id == pendingId).ApprovedAmount);
        Assert.Contains(claim.Id, _notifier.Completed);
    }

    // ---- Admin/HR escalation to Top-Level (skips Finance review) ----

    [Fact]
    public async Task Admin_hr_can_forward_directly_to_top_level()
    {
        var claim = SeedClaim(ApprovalStage.AdminHr, 1_000m);

        await _service.DecideAsync(claim.Id, AdminHr,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) }, forwardToTopLevel: true);

        Assert.Equal(ApprovalStage.TopLevel, claim.Stage);
        Assert.True(claim.EscalatedToTopLevel);
        Assert.Contains("Top-Level", claim.History[^1].Summary);
        Assert.Contains((claim.Id, ApprovalStage.TopLevel), _notifier.Awaiting);
    }

    [Fact]
    public async Task Only_admin_hr_may_use_the_escalation_flag()
    {
        var claim = SeedClaim(ApprovalStage.LineManager, 1_000m);

        await Assert.ThrowsAsync<WorkflowConflictException>(() =>
            _service.DecideAsync(claim.Id, LineManager,
                new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) }, forwardToTopLevel: true));
    }

    [Fact]
    public async Task The_escalation_flag_is_reassigned_on_every_admin_hr_round()
    {
        // A stale true flag (e.g. left from an earlier round) must not survive an Admin/HR
        // decision that does NOT forward.
        var claim = SeedClaim(ApprovalStage.AdminHr, 1_000m);
        claim.EscalatedToTopLevel = true;

        await _service.DecideAsync(claim.Id, AdminHr,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) });

        Assert.False(claim.EscalatedToTopLevel);
        Assert.Equal(ApprovalStage.Finance, claim.Stage);
    }

    // ---- Grade-routed Finance authorities ----

    [Fact]
    public async Task M_grade_claims_are_decidable_only_by_the_m_grade_finance_authority()
    {
        var claim = SeedClaim(ApprovalStage.Finance, 1_000m); // DEMO-M-002 → MStandard
        var decision = new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) };

        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => _service.DecideAsync(claim.Id, FinanceOther, decision));
        await _service.DecideAsync(claim.Id, FinanceMGrade, decision);
        Assert.Equal(ApprovalStage.Completed, claim.Stage);
    }

    [Fact]
    public async Task Executive_claims_are_decidable_only_by_the_other_finance_authority()
    {
        var claim = SeedClaim(ApprovalStage.Finance, 1_000m, employeeId: "DEMO-E-001");
        var decision = new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) };

        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => _service.DecideAsync(claim.Id, FinanceMGrade, decision));
        await _service.DecideAsync(claim.Id, FinanceOther, decision);
        Assert.Equal(ApprovalStage.Completed, claim.Stage);
    }

    [Fact]
    public async Task Finance_queues_are_filtered_by_grade_bucket()
    {
        var mClaim = SeedClaim(ApprovalStage.Finance, 1_000m);
        var eClaim = SeedClaim(ApprovalStage.Finance, 1_000m, employeeId: "DEMO-E-001");

        var mQueue = await _service.GetQueueForRoleAsync(FinanceMGrade);
        var otherQueue = await _service.GetQueueForRoleAsync(FinanceOther);

        Assert.Contains(mQueue, c => c.Id == mClaim.Id);
        Assert.DoesNotContain(mQueue, c => c.Id == eClaim.Id);
        Assert.Contains(otherQueue, c => c.Id == eClaim.Id);
        Assert.DoesNotContain(otherQueue, c => c.Id == mClaim.Id);
    }

    [Fact]
    public async Task Posting_is_gated_to_the_routed_finance_authority()
    {
        var claim = SeedClaim(ApprovalStage.Finance, 1_000m);
        await _service.DecideAsync(claim.Id, FinanceMGrade,
            new[] { new LineDecisionInput(claim.Lines[0].Id, "approve", null, null) });
        Assert.Equal(ApprovalStage.Completed, claim.Stage);

        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => _service.PostAsync(claim.Id, FinanceOther, "AR-2026-000200"));
        await _service.PostAsync(claim.Id, FinanceMGrade, "AR-2026-000200");
        Assert.True(claim.Posted);
        Assert.Contains(claim.Id, _notifier.Posted);
    }

    // ---- helpers / fakes ----

    private static SubmitClaimRequest Request(string employeeId, Guid receiptId, string? comment = null) => new(
        Guid.NewGuid(), employeeId, new DateOnly(2026, 7, 23),
        new[]
        {
            new SubmitClaimLine(Guid.NewGuid(), BeneficiaryKind.Self, null, null, ClaimCategory.Opd,
                new DateOnly(2026, 7, 20), 1_000m, "receipt.jpg", new[] { receiptId }, comment),
        });

    /// <summary>A two-line claim decided at Line Manager: line0 approved (1000), line1 rejected (2000).</summary>
    private async Task<ClaimRecord> ReturnedTwoLineClaim()
    {
        var claim = SeedTwoLineClaim();
        await _service.DecideAsync(claim.Id, LineManager, new[]
        {
            new LineDecisionInput(claim.Lines[0].Id, "approve", null, null),
            new LineDecisionInput(claim.Lines[1].Id, "reject", null, "Blurry receipt"),
        });
        Assert.Equal(ApprovalStage.ReturnedToEmployee, claim.Stage);
        return claim;
    }

    private static SubmitClaimRequest EditRequest(ClaimRecord claim, params SubmitClaimLine[] lines)
        => new(claim.Id, claim.EmployeeId, new DateOnly(2026, 7, 23), lines);

    /// <summary>Rebuild the outbound line from the stored record, optionally changing fields.</summary>
    private static SubmitClaimLine LineFrom(
        ClaimLineRecord stored, decimal? newAmount = null, Guid? newReceiptId = null, string? comment = null)
        => new(stored.Id,
            Enum.Parse<BeneficiaryKind>(stored.BeneficiaryKind),
            stored.DependantId,
            stored.DependantRelationship is null ? null : Enum.Parse<DependantRelationship>(stored.DependantRelationship),
            Enum.Parse<ClaimCategory>(stored.Category),
            stored.ExpenseDate,
            newAmount ?? stored.ClaimedAmount,
            stored.ReceiptReference,
            newReceiptId is { } id ? new[] { id } : stored.ReceiptIds.ToArray(),
            comment);

    private ClaimRecord SeedTwoLineClaim(string employeeId = "DEMO-M-002")
    {
        var claimId = Guid.NewGuid();
        var claim = new ClaimRecord
        {
            Id = claimId,
            EmployeeId = employeeId,
            SubmissionDate = new DateOnly(2026, 7, 23),
            Stage = ApprovalStage.LineManager,
            Lines = new List<ClaimLineRecord>
            {
                new()
                {
                    Id = Guid.NewGuid(), ClaimId = claimId, Category = "Opd",
                    ExpenseDate = new DateOnly(2026, 7, 20), ReceiptReference = "a.jpg",
                    ClaimedAmount = 1_000m, CurrentAmount = 1_000m, Status = LineDecisionStatus.Pending,
                },
                new()
                {
                    Id = Guid.NewGuid(), ClaimId = claimId, Category = "Dental",
                    ExpenseDate = new DateOnly(2026, 7, 21), ReceiptReference = "b.jpg",
                    ClaimedAmount = 2_000m, CurrentAmount = 2_000m, Status = LineDecisionStatus.Pending,
                },
            },
        };
        _claims.Store[claim.Id] = claim;
        return claim;
    }

    private ClaimRecord SeedClaim(ApprovalStage stage, decimal amount, string employeeId = "DEMO-M-002")
    {
        var claim = new ClaimRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            SubmissionDate = new DateOnly(2026, 7, 23),
            Stage = stage,
            Lines = new List<ClaimLineRecord>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Category = "Opd",
                    ExpenseDate = new DateOnly(2026, 7, 20),
                    ReceiptReference = "r.jpg",
                    ClaimedAmount = amount,
                    CurrentAmount = amount,
                    Status = LineDecisionStatus.Pending,
                },
            },
        };
        _claims.Store[claim.Id] = claim;
        return claim;
    }

    private sealed class FakeClaimRepository : IClaimRepository
    {
        public Dictionary<Guid, ClaimRecord> Store { get; } = new();

        public Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.GetValueOrDefault(id));

        public Task<IReadOnlyList<ClaimRecord>> GetByEmployeeAsync(string employeeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(Store.Values.Where(c => c.EmployeeId == employeeId).ToList());

        public Task<IReadOnlyList<ClaimRecord>> GetByStageAsync(ApprovalStage stage, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(Store.Values.Where(c => c.Stage == stage).ToList());

        public Task<IReadOnlyList<ClaimRecord>> GetPostableAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(
                Store.Values.Where(c => c.Stage == ApprovalStage.Completed && !c.Posted).ToList());

        public Task AddAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Store[claim.Id] = claim;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class FakeProfileProvider : IEmployeeEntitlementProfileProvider
    {
        private readonly Dictionary<string, EmployeeEntitlementProfile> _profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEMO-M-002"] = new("DEMO-M-002", GradeBand.MStandard, Money.FromRupees(33_333.33m), new DateOnly(2019, 3, 1)),
            ["DEMO-E-001"] = new("DEMO-E-001", GradeBand.Executive, Money.FromRupees(150_000m), new DateOnly(2015, 1, 10)),
        };

        public Task<EmployeeEntitlementProfile?> GetProfileAsync(string employeeId, CancellationToken ct = default)
            => Task.FromResult(_profiles.GetValueOrDefault(employeeId));
    }

    internal sealed class RecordingNotifier : IClaimNotifier
    {
        public List<(Guid ClaimId, ApprovalStage Stage)> Awaiting { get; } = new();
        public List<Guid> Returned { get; } = new();
        public List<Guid> Completed { get; } = new();
        public List<Guid> Posted { get; } = new();

        public Task ClaimAwaitingStageAsync(ClaimRecord claim, ApprovalStage stage, CancellationToken ct = default)
        {
            Awaiting.Add((claim.Id, stage));
            return Task.CompletedTask;
        }

        public Task ClaimReturnedAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Returned.Add(claim.Id);
            return Task.CompletedTask;
        }

        public List<Guid> Closed { get; } = new();

        public Task ClaimClosedAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Closed.Add(claim.Id);
            return Task.CompletedTask;
        }

        public Task ClaimCompletedAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Completed.Add(claim.Id);
            return Task.CompletedTask;
        }

        public Task ClaimPostedAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Posted.Add(claim.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceiptRepository : IReceiptRepository
    {
        private readonly Dictionary<Guid, ReceiptRecord> _store = new();

        public Guid Seed(string employeeId)
        {
            var record = new ReceiptRecord { Id = Guid.NewGuid(), UploadedByEmployeeId = employeeId };
            _store[record.Id] = record;
            return record.Id;
        }

        public Task AddAsync(ReceiptRecord receipt, CancellationToken ct = default)
        {
            _store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }

        public Task<ReceiptRecord?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Guid>> GetOwnedExistingIdsAsync(
            IReadOnlyCollection<Guid> ids, string employeeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(
                _store.Values.Where(r => ids.Contains(r.Id) && r.UploadedByEmployeeId == employeeId).Select(r => r.Id).ToList());

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
