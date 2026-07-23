using FluentValidation;
using FluentValidation.Results;
using Mems.Application.Auth;
using Mems.Application.Claims;
using Mems.Application.Notifications;
using Mems.Application.Receipts;

namespace Mems.Application.Workflow;

/// <summary>
/// The claim lifecycle: submit → the sequential approval chain → completed → (manual SAP posting).
/// Enforces per-line, per-stage decisions and the editing rules (docs/entitlement-rules.md
/// §§ Claim structure, Approval flow, Editing rules). All state transitions and the append-only
/// audit trail live here so the rules are in one auditable place.
///
/// Lock semantics (decision 2026-07-23): when a claim is returned because a line was rejected,
/// the lines already Approved/Reduced are LOCKED — the employee cannot change them and no stage
/// re-decides them. Only the previously rejected lines are editable and restart the chain.
/// </summary>
public sealed class ClaimWorkflowService
{
    private readonly IClaimRepository _repo;
    private readonly IReceiptRepository _receipts;
    private readonly IValidator<SubmitClaimRequest> _validator;
    private readonly FinanceRoutingService _financeRouting;
    private readonly IClaimNotifier _notifier;
    private readonly decimal _threshold;

    public ClaimWorkflowService(
        IClaimRepository repo,
        IReceiptRepository receipts,
        IValidator<SubmitClaimRequest> validator,
        ApprovalOptions options,
        FinanceRoutingService financeRouting,
        IClaimNotifier notifier)
    {
        _repo = repo;
        _receipts = receipts;
        _validator = validator;
        _financeRouting = financeRouting;
        _notifier = notifier;
        _threshold = options.TopLevelThreshold;
    }

    // ---- Employee actions ----

    /// <summary>Submit a claim. Idempotent on the client UUID: a retry returns the existing claim.</summary>
    public async Task<ClaimRecord> SubmitAsync(AuthUser actor, SubmitClaimRequest request, CancellationToken ct = default)
    {
        var employeeId = actor.EmployeeId
            ?? throw new WorkflowForbiddenException("Only employees submit claims.");
        _validator.ValidateAndThrow(request);

        var existing = await _repo.GetAsync(request.ClaimId, ct);
        if (existing is not null) return existing; // duplicate submit (offline retry) — no-op

        await EnsureReceiptsOwnedAsync(employeeId, request.Lines, ct);

        var now = DateTime.UtcNow;
        var claim = new ClaimRecord
        {
            Id = request.ClaimId,
            EmployeeId = employeeId,
            SubmissionDate = request.SubmissionDate,
            Stage = ApprovalStage.LineManager,
            CreatedAt = now,
            UpdatedAt = now,
            // Pre-set line ids: this whole graph goes through Add (everything inserts), and the
            // Submitted events below need the ids up front. The "leave keys default" rule only
            // applies when appending children to an ALREADY-TRACKED claim.
            Lines = request.Lines.Select(l => ToLine(l, assignId: true)).ToList(),
        };
        for (var i = 0; i < claim.Lines.Count; i++)
        {
            claim.LineEvents.Add(LineEvent(claim.Id, claim.Lines[i].Id, ApprovalStage.LineManager,
                "Submitted", "Employee", actor.DisplayName, claim.Lines[i].ClaimedAmount,
                reason: null, comment: request.Lines[i].Comment));
        }

        await _repo.AddAsync(claim, ct);
        await _repo.SaveChangesAsync(ct);

        await _notifier.ClaimAwaitingStageAsync(claim, ApprovalStage.LineManager, ct);
        return claim;
    }

    /// <summary>
    /// Edit &amp; resubmit an editable claim. A never-reviewed claim can be reshaped freely. A
    /// RETURNED claim is different: locked (Approved/Reduced) lines must come back unchanged, no
    /// lines may be added or removed, and only the previously rejected lines restart the chain.
    /// History is preserved either way.
    /// </summary>
    public async Task<ClaimRecord> EditAsync(Guid claimId, AuthUser actor, SubmitClaimRequest request, CancellationToken ct = default)
    {
        var employeeId = actor.EmployeeId
            ?? throw new WorkflowForbiddenException("Only employees edit claims.");
        var claim = await Require(claimId, ct);

        if (!claim.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase))
            throw new WorkflowForbiddenException("You can only edit your own claims.");
        if (!ClaimMapper.Editable(claim))
            throw new WorkflowConflictException("This claim can no longer be edited.");

        if (claim.Stage == ApprovalStage.ReturnedToEmployee)
            await ApplyReturnedEditAsync(claim, actor, request, ct);
        else
            await ApplyFreshEditAsync(claim, actor, request, ct);

        claim.Stage = ApprovalStage.LineManager;
        claim.UpdatedAt = DateTime.UtcNow;
        // Resubmission restarts the chain (for the editable lines) but does NOT erase prior
        // history (audit requirement).
        claim.History.Add(Event(claim, ApprovalStage.LineManager, "Employee", "Employee", "Edited and resubmitted."));
        await _repo.SaveChangesAsync(ct);

        await _notifier.ClaimAwaitingStageAsync(claim, ApprovalStage.LineManager, ct);
        return claim;
    }

    /// <summary>Free-form edit of a claim no stage has touched: full replace, add/remove allowed.</summary>
    private async Task ApplyFreshEditAsync(ClaimRecord claim, AuthUser actor, SubmitClaimRequest request, CancellationToken ct)
    {
        _validator.ValidateAndThrow(request);
        await EnsureReceiptsOwnedAsync(claim.EmployeeId, request.Lines, ct);

        claim.Lines.Clear();
        claim.Lines.AddRange(request.Lines.Select(l => ToLine(l, assignId: false)));
        // The old lines (and their ids) are gone, so their Submitted events point nowhere —
        // rewrite them. Acceptable append-only exception: no stage ever saw this draft.
        claim.LineEvents.Clear();

        // New line ids are EF-generated on insert (lines joined an already-tracked claim, where a
        // pre-set Guid key would be mistaken for an existing row) — save first, then write the
        // Submitted events with the real ids.
        await _repo.SaveChangesAsync(ct);
        for (var i = 0; i < claim.Lines.Count; i++)
        {
            claim.LineEvents.Add(LineEvent(claim.Id, claim.Lines[i].Id, ApprovalStage.LineManager,
                "Submitted", "Employee", actor.DisplayName, claim.Lines[i].ClaimedAmount,
                reason: null, comment: request.Lines[i].Comment));
        }
    }

    /// <summary>Resubmit of a returned claim: rejected lines only; locked lines must be untouched.</summary>
    private async Task ApplyReturnedEditAsync(ClaimRecord claim, AuthUser actor, SubmitClaimRequest request, CancellationToken ct)
    {
        var incomingById = request.Lines.ToDictionary(l => l.LineId);
        var storedIds = claim.Lines.Select(l => l.Id).ToHashSet();
        if (incomingById.Count != claim.Lines.Count || !storedIds.SetEquals(incomingById.Keys))
            throw new WorkflowConflictException("Lines cannot be added or removed when resubmitting a returned claim.");

        var editable = new List<(ClaimLineRecord Stored, SubmitClaimLine Incoming)>();
        foreach (var stored in claim.Lines)
        {
            var incoming = incomingById[stored.Id];
            if (stored.Status is LineDecisionStatus.Approved or LineDecisionStatus.Reduced)
            {
                if (LockedLineChanged(stored, incoming))
                    throw new WorkflowConflictException("Approved items are locked and cannot be changed — edit only the rejected items.");
                // Normalize: a line Reduced at the rejecting stage still carries its pre-reduction
                // CurrentAmount (the carry-forward loop never ran). Fix it here or every total
                // (threshold, queue sums) overcounts from now on.
                stored.CurrentAmount = stored.ApprovedAmount!.Value;
            }
            else
            {
                editable.Add((stored, incoming));
            }
        }

        // Validate only the resubmitted lines: locked lines are untouched by design and may
        // predate rules added later (e.g. pre-image-upload lines with no ReceiptId).
        var editableLines = editable.Select(e => e.Incoming).ToList();
        _validator.ValidateAndThrow(request with { Lines = editableLines });
        await EnsureReceiptsOwnedAsync(claim.EmployeeId, editableLines, ct);

        foreach (var (stored, incoming) in editable)
        {
            stored.BeneficiaryKind = incoming.BeneficiaryKind.ToString();
            stored.DependantId = incoming.DependantId;
            stored.DependantRelationship = incoming.DependantRelationship?.ToString();
            stored.Category = incoming.Category.ToString();
            stored.ExpenseDate = incoming.ExpenseDate;
            stored.ReceiptReference = incoming.ReceiptReference;
            // New list instance (not in-place mutation) so the EF value comparer sees the change.
            stored.ReceiptIds = incoming.ReceiptIds?.ToList() ?? new List<Guid>();
            stored.ClaimedAmount = incoming.ClaimedAmount;
            stored.CurrentAmount = incoming.ClaimedAmount;
            stored.ApprovedAmount = null;
            stored.Status = LineDecisionStatus.Pending;
            stored.RejectionReason = null;

            claim.LineEvents.Add(LineEvent(claim.Id, stored.Id, ApprovalStage.LineManager,
                "Resubmitted", "Employee", actor.DisplayName, incoming.ClaimedAmount,
                reason: null, comment: incoming.Comment));
        }
    }

    /// <summary>The fields an employee could meaningfully change — a locked line must keep them all.</summary>
    private static bool LockedLineChanged(ClaimLineRecord stored, SubmitClaimLine incoming)
        => stored.BeneficiaryKind != incoming.BeneficiaryKind.ToString()
           || stored.DependantId != incoming.DependantId
           || stored.Category != incoming.Category.ToString()
           || stored.ExpenseDate != incoming.ExpenseDate
           || stored.ClaimedAmount != incoming.ClaimedAmount
           // Set equality (order-insensitive), normalizing a missing wire list to empty so a
           // locked legacy line (no stored images) round-trips without a false 409.
           || !stored.ReceiptIds.ToHashSet().SetEquals((IEnumerable<Guid>?)incoming.ReceiptIds ?? Array.Empty<Guid>());

    public Task<IReadOnlyList<ClaimRecord>> GetForEmployeeAsync(string employeeId, CancellationToken ct = default)
        => _repo.GetByEmployeeAsync(employeeId, ct);

    public Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    // ---- Approver actions ----

    public async Task<IReadOnlyList<ClaimRecord>> GetQueueForRoleAsync(AuthUser actor, CancellationToken ct = default)
    {
        var stage = ApprovalStagePolicy.StageForRole(actor.Role)
            ?? throw new WorkflowForbiddenException("This role has no approval queue.");

        var awaiting = await _repo.GetByStageAsync(stage, ct);
        if (actor.Role != UserRole.Finance) return awaiting;

        // Finance is split into two grade-routed authorities: each sees only their own claims,
        // both to review and to post.
        var postable = await _repo.GetPostableAsync(ct);
        var mine = new List<ClaimRecord>();
        foreach (var claim in awaiting.Concat(postable))
        {
            if (await _financeRouting.IsRequiredFinanceAsync(claim.EmployeeId, actor.Email, ct))
                mine.Add(claim);
        }
        return mine;
    }

    /// <summary>
    /// Apply the current stage's decision to every PENDING line (locked lines were decided in an
    /// earlier round and are never re-decided), then either return the claim to the employee (any
    /// rejection) or advance to the next stage / completion.
    /// </summary>
    public async Task<ClaimRecord> DecideAsync(
        Guid claimId, AuthUser actor, IReadOnlyList<LineDecisionInput> decisions,
        bool forwardToTopLevel = false, CancellationToken ct = default)
    {
        var claim = await Require(claimId, ct);

        var owningRole = ApprovalStagePolicy.RoleForStage(claim.Stage);
        if (owningRole is null)
            throw new WorkflowConflictException("This claim is not awaiting an approval decision.");
        if (actor.Role != owningRole.Value)
            throw new WorkflowForbiddenException($"This claim is awaiting {ApprovalStagePolicy.StageLabel(claim.Stage)}, not you.");
        if (claim.Stage == ApprovalStage.Finance
            && !await _financeRouting.IsRequiredFinanceAsync(claim.EmployeeId, actor.Email, ct))
        {
            var required = await _financeRouting.RequiredFinanceEmailAsync(claim.EmployeeId, ct);
            throw new WorkflowForbiddenException($"This claim's Finance review is handled by {required}.");
        }

        // Escalation is an Admin/HR capability only (docs/entitlement-rules.md § Approval flow).
        if (forwardToTopLevel && claim.Stage != ApprovalStage.AdminHr)
            throw new WorkflowConflictException("Only Admin/HR can forward a claim directly to the Top-Level Approver.");

        // Stage capability: the Line Manager approves in full or rejects — never reduces.
        // Checked before the apply loop so a forbidden request mutates nothing.
        if (!ApprovalStagePolicy.CanReduce(claim.Stage)
            && decisions.Any(d => string.Equals(d.Action, "reduce", StringComparison.OrdinalIgnoreCase)))
            throw new WorkflowForbiddenException(
                "The Line Manager approves in full or rejects with a reason — amounts can only be reduced at later stages.");

        var pendingLines = claim.Lines.Where(l => l.Status == LineDecisionStatus.Pending).ToList();
        if (pendingLines.Count == 0)
            throw new WorkflowConflictException("This claim has no lines awaiting a decision."); // defensive

        var byLine = decisions.ToDictionary(d => d.LineId);
        if (decisions.Any(d => pendingLines.All(l => l.Id != d.LineId)))
            throw new WorkflowConflictException("A decision was supplied for a line that is not awaiting one (locked lines are decided already).");

        foreach (var line in pendingLines)
        {
            if (!byLine.TryGetValue(line.Id, out var d))
                throw new WorkflowConflictException("Every pending line must have a decision.");
            ApplyDecision(line, d, topLevelAdjust: claim.Stage == ApprovalStage.TopLevel);
            claim.LineEvents.Add(LineEvent(claim.Id, line.Id, claim.Stage,
                line.Status.ToString(), ApprovalStagePolicy.RoleLabel(actor.Role), actor.DisplayName,
                line.Status == LineDecisionStatus.Rejected ? null : line.ApprovedAmount,
                reason: line.RejectionReason, comment: d.Comment));
        }

        var approved = pendingLines.Count(l => l.Status == LineDecisionStatus.Approved);
        var reduced = pendingLines.Count(l => l.Status == LineDecisionStatus.Reduced);
        var rejected = pendingLines.Count(l => l.Status == LineDecisionStatus.Rejected);
        var actedStage = claim.Stage;

        if (actedStage == ApprovalStage.TopLevel)
        {
            // Top-Level decisions are FINAL (decision 2026-07-23): rejections never return the
            // claim; rejected items stay rejected permanently. If anything was approved — this
            // round or a locked earlier round — the claim completes and Finance posts it;
            // otherwise it closes with nothing to post. Lines are never re-opened.
            foreach (var line in pendingLines.Where(l => l.Status != LineDecisionStatus.Rejected))
                line.CurrentAmount = line.ApprovedAmount!.Value;
            claim.Stage = claim.Lines.Any(l => (l.ApprovedAmount ?? 0m) > 0m)
                ? ApprovalStage.Completed
                : ApprovalStage.Closed;
        }
        else if (rejected > 0)
        {
            // Any rejection sends the whole claim back to the employee to fix and resubmit.
            claim.Stage = ApprovalStage.ReturnedToEmployee;
        }
        else
        {
            // Carry each newly decided amount forward as the amount the next stage will see.
            // Locked lines already carry their final amount in CurrentAmount.
            foreach (var line in pendingLines) line.CurrentAmount = line.ApprovedAmount!.Value;
            var approvedTotal = claim.Lines.Sum(l => l.CurrentAmount);

            if (actedStage == ApprovalStage.AdminHr)
                claim.EscalatedToTopLevel = forwardToTopLevel; // assign, never OR — stale escalation must not survive
            var next = ApprovalStagePolicy.NextStage(actedStage, approvedTotal, _threshold, claim.EscalatedToTopLevel);

            if (next != ApprovalStage.Completed)
            {
                // Re-open only the freshly decided lines for the next stage; locked lines stay decided.
                foreach (var line in pendingLines)
                {
                    line.Status = LineDecisionStatus.Pending;
                    line.ApprovedAmount = null;
                }
            }
            claim.Stage = next;
        }

        claim.UpdatedAt = DateTime.UtcNow;
        var escalationNote = actedStage == ApprovalStage.AdminHr && claim.EscalatedToTopLevel && rejected == 0
            ? " Forwarded directly to Top-Level Approver (skipping Finance review)."
            : "";
        var finalityNote = actedStage == ApprovalStage.TopLevel && rejected > 0
            ? " Rejected items are final (Top-Level decision)."
            : "";
        claim.History.Add(Event(claim, actedStage, ApprovalStagePolicy.RoleLabel(actor.Role), actor.DisplayName,
            $"Approved {approved}, reduced {reduced}, rejected {rejected}."
            + (claim.Stage == ApprovalStage.ReturnedToEmployee
                ? " Returned to employee."
                : $" → {ApprovalStagePolicy.StageLabel(claim.Stage)}.")
            + escalationNote + finalityNote));

        await _repo.SaveChangesAsync(ct);

        if (claim.Stage == ApprovalStage.Closed) await _notifier.ClaimClosedAsync(claim, ct);
        else if (claim.Stage == ApprovalStage.ReturnedToEmployee) await _notifier.ClaimReturnedAsync(claim, ct);
        else if (claim.Stage == ApprovalStage.Completed) await _notifier.ClaimCompletedAsync(claim, ct);
        else await _notifier.ClaimAwaitingStageAsync(claim, claim.Stage, ct);
        return claim;
    }

    /// <summary>
    /// Finance records the manual SAP posting reference once a claim is fully approved. The SAP
    /// reference is mandatory (CLAUDE.md §7/§13) — an empty value defeats the audit trail.
    /// </summary>
    public async Task<ClaimRecord> PostAsync(Guid claimId, AuthUser actor, string sapReference, CancellationToken ct = default)
    {
        if (actor.Role != UserRole.Finance)
            throw new WorkflowForbiddenException("Only Finance records the SAP posting.");
        if (string.IsNullOrWhiteSpace(sapReference))
            throw new WorkflowConflictException("A SAP reference number is required to mark a claim posted.");

        var claim = await Require(claimId, ct);
        if (!await _financeRouting.IsRequiredFinanceAsync(claim.EmployeeId, actor.Email, ct))
        {
            var required = await _financeRouting.RequiredFinanceEmailAsync(claim.EmployeeId, ct);
            throw new WorkflowForbiddenException($"This claim's SAP posting is handled by {required}.");
        }
        if (claim.Stage != ApprovalStage.Completed || claim.Posted)
            throw new WorkflowConflictException("Only a completed, not-yet-posted claim can be posted.");

        claim.Posted = true;
        claim.SapReference = sapReference.Trim();
        claim.UpdatedAt = DateTime.UtcNow;
        claim.History.Add(Event(claim, ApprovalStage.Completed, "Finance", actor.DisplayName,
            $"Recorded SAP posting (ref {claim.SapReference})."));

        await _repo.SaveChangesAsync(ct);

        await _notifier.ClaimPostedAsync(claim, ct);
        return claim;
    }

    // ---- helpers ----

    private async Task<ClaimRecord> Require(Guid id, CancellationToken ct)
        => await _repo.GetAsync(id, ct) ?? throw new ClaimNotFoundException(id);

    /// <summary>
    /// Every ReceiptId in the payload must be a real upload owned by the submitting employee —
    /// a forged/stale GUID must not satisfy the "receipt required" rule.
    /// </summary>
    private async Task EnsureReceiptsOwnedAsync(string employeeId, IReadOnlyList<SubmitClaimLine> lines, CancellationToken ct)
    {
        var ids = lines.SelectMany(l => l.ReceiptIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0) return; // validator already rejects missing ids

        var owned = await _receipts.GetOwnedExistingIdsAsync(ids, employeeId, ct);
        if (owned.Count != ids.Count)
            throw new ValidationException(new[]
            {
                new ValidationFailure("lines",
                    "One or more receipt attachments were not found. Please re-attach the receipt images and try again."),
            });
    }

    private static void ApplyDecision(ClaimLineRecord line, LineDecisionInput d, bool topLevelAdjust)
    {
        switch (d.Action?.ToLowerInvariant())
        {
            case "approve":
                line.ApprovedAmount = line.CurrentAmount;
                line.Status = LineDecisionStatus.Approved;
                line.RejectionReason = null;
                break;
            case "reduce":
                // Earlier stages may only REDUCE (strictly below the carried amount). The
                // Top-Level Approver may ADJUST to any amount up to the ORIGINAL claim —
                // including increasing back above a reduction (decision 2026-07-23).
                if (d.Amount is not { } amt || amt <= 0m || (topLevelAdjust ? amt > line.ClaimedAmount : amt >= line.CurrentAmount))
                    throw new WorkflowConflictException(topLevelAdjust
                        ? "The adjusted amount must be greater than zero and no more than the originally claimed amount."
                        : "A reduced amount must be greater than zero and less than the current amount.");
                // Changing an amount always needs a stated reason for the audit trail.
                if (string.IsNullOrWhiteSpace(d.Comment))
                    throw new WorkflowConflictException("Changing the amount requires a comment.");
                line.ApprovedAmount = amt;
                // Status is relative to the ORIGINAL claim: a Top-Level adjust back to the full
                // claimed amount reads as Approved.
                line.Status = topLevelAdjust && amt == line.ClaimedAmount
                    ? LineDecisionStatus.Approved
                    : LineDecisionStatus.Reduced;
                line.RejectionReason = null;
                break;
            case "reject":
                if (string.IsNullOrWhiteSpace(d.Reason))
                    throw new WorkflowConflictException("A rejection needs a reason.");
                line.ApprovedAmount = 0m;
                line.Status = LineDecisionStatus.Rejected;
                line.RejectionReason = d.Reason.Trim();
                break;
            default:
                throw new WorkflowConflictException($"Unknown decision action '{d.Action}'.");
        }
    }

    private static ClaimLineRecord ToLine(SubmitClaimLine input, bool assignId) => new()
    {
        // assignId only when the line is part of a brand-new claim graph (everything inserts).
        // On an already-tracked claim a pre-set Guid key makes EF treat the line as existing →
        // a bogus UPDATE — there, leave the key default so EF generates it on insert.
        Id = assignId ? Guid.NewGuid() : default,
        BeneficiaryKind = input.BeneficiaryKind.ToString(),
        DependantId = input.DependantId,
        DependantRelationship = input.DependantRelationship?.ToString(),
        Category = input.Category.ToString(),
        ExpenseDate = input.ExpenseDate,
        ReceiptReference = input.ReceiptReference,
        ReceiptIds = input.ReceiptIds?.ToList() ?? new List<Guid>(),
        ClaimedAmount = input.ClaimedAmount,
        CurrentAmount = input.ClaimedAmount,
        ApprovedAmount = null,
        Status = LineDecisionStatus.Pending,
    };

    private static ClaimLineEventRecord LineEvent(
        Guid claimId, Guid lineId, ApprovalStage stage, string action, string actorRole,
        string actorName, decimal? amount, string? reason, string? comment) => new()
    {
        // Id left default so EF inserts (not updates) this new audit row on a tracked claim.
        ClaimId = claimId,
        LineId = lineId,
        Stage = stage,
        Action = action,
        ActorRole = actorRole,
        ActorName = actorName,
        Amount = amount,
        Reason = reason,
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
        At = DateTime.UtcNow,
    };

    private static ApprovalEvent Event(ClaimRecord claim, ApprovalStage stage, string role, string name, string summary) => new()
    {
        // Id left default so EF inserts (not updates) this new audit row on an existing claim.
        ClaimId = claim.Id,
        Stage = stage,
        ActorRole = role,
        ActorName = name,
        Summary = summary,
        At = DateTime.UtcNow,
    };
}
