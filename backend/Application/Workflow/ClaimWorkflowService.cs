using FluentValidation;
using Mems.Application.Auth;
using Mems.Application.Claims;

namespace Mems.Application.Workflow;

/// <summary>
/// The claim lifecycle: submit → the sequential approval chain → completed → (manual SAP posting).
/// Enforces per-line, per-stage decisions and the editing rules (docs/entitlement-rules.md
/// §§ Claim structure, Approval flow, Editing rules). All state transitions and the append-only
/// audit trail live here so the rules are in one auditable place.
/// </summary>
public sealed class ClaimWorkflowService
{
    private readonly IClaimRepository _repo;
    private readonly IValidator<SubmitClaimRequest> _validator;
    private readonly decimal _threshold;

    public ClaimWorkflowService(
        IClaimRepository repo,
        IValidator<SubmitClaimRequest> validator,
        ApprovalOptions options)
    {
        _repo = repo;
        _validator = validator;
        _threshold = options.TopLevelThreshold;
    }

    // ---- Employee actions ----

    /// <summary>Submit a claim. Idempotent on the client UUID: a retry returns the existing claim.</summary>
    public async Task<ClaimRecord> SubmitAsync(string employeeId, SubmitClaimRequest request, CancellationToken ct = default)
    {
        _validator.ValidateAndThrow(request);

        var existing = await _repo.GetAsync(request.ClaimId, ct);
        if (existing is not null) return existing; // duplicate submit (offline retry) — no-op

        var now = DateTime.UtcNow;
        var claim = new ClaimRecord
        {
            Id = request.ClaimId,
            EmployeeId = employeeId,
            SubmissionDate = request.SubmissionDate,
            Stage = ApprovalStage.LineManager,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = request.Lines.Select(ToLine).ToList(),
        };

        await _repo.AddAsync(claim, ct);
        await _repo.SaveChangesAsync(ct);
        return claim;
    }

    /// <summary>Edit &amp; resubmit an editable claim. History is preserved; it restarts at Line Manager.</summary>
    public async Task<ClaimRecord> EditAsync(Guid claimId, string employeeId, SubmitClaimRequest request, CancellationToken ct = default)
    {
        _validator.ValidateAndThrow(request);
        var claim = await Require(claimId, ct);

        if (!claim.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase))
            throw new WorkflowForbiddenException("You can only edit your own claims.");
        if (!ClaimMapper.Editable(claim))
            throw new WorkflowConflictException("This claim can no longer be edited.");

        claim.Lines.Clear();
        claim.Lines.AddRange(request.Lines.Select(ToLine));
        claim.Stage = ApprovalStage.LineManager;
        claim.UpdatedAt = DateTime.UtcNow;
        // Resubmission restarts the whole chain but does NOT erase prior history (audit requirement).
        claim.History.Add(Event(claim, ApprovalStage.LineManager, "Employee", "Employee", "Edited and resubmitted."));

        await _repo.SaveChangesAsync(ct);
        return claim;
    }

    public Task<IReadOnlyList<ClaimRecord>> GetForEmployeeAsync(string employeeId, CancellationToken ct = default)
        => _repo.GetByEmployeeAsync(employeeId, ct);

    public Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    // ---- Approver actions ----

    public async Task<IReadOnlyList<ClaimRecord>> GetQueueForRoleAsync(UserRole role, CancellationToken ct = default)
    {
        var stage = ApprovalStagePolicy.StageForRole(role)
            ?? throw new WorkflowForbiddenException("This role has no approval queue.");

        var awaiting = await _repo.GetByStageAsync(stage, ct);
        if (role != UserRole.Finance) return awaiting;

        // Finance also owns the posting worklist: completed claims not yet recorded in SAP.
        var postable = await _repo.GetPostableAsync(ct);
        return awaiting.Concat(postable).ToList();
    }

    /// <summary>
    /// Apply the current stage's decision to every line, then either return the claim to the
    /// employee (any rejection) or advance to the next stage / completion.
    /// </summary>
    public async Task<ClaimRecord> DecideAsync(Guid claimId, AuthUser actor, IReadOnlyList<LineDecisionInput> decisions, CancellationToken ct = default)
    {
        var claim = await Require(claimId, ct);

        var owningRole = ApprovalStagePolicy.RoleForStage(claim.Stage);
        if (owningRole is null)
            throw new WorkflowConflictException("This claim is not awaiting an approval decision.");
        if (actor.Role != owningRole.Value)
            throw new WorkflowForbiddenException($"This claim is awaiting {ApprovalStagePolicy.StageLabel(claim.Stage)}, not you.");

        var byLine = decisions.ToDictionary(d => d.LineId);
        foreach (var line in claim.Lines)
        {
            if (!byLine.TryGetValue(line.Id, out var d))
                throw new WorkflowConflictException("Every line must have a decision.");
            ApplyDecision(line, d);
        }

        var approved = claim.Lines.Count(l => l.Status == LineDecisionStatus.Approved);
        var reduced = claim.Lines.Count(l => l.Status == LineDecisionStatus.Reduced);
        var rejected = claim.Lines.Count(l => l.Status == LineDecisionStatus.Rejected);
        var actedStage = claim.Stage;

        if (rejected > 0)
        {
            // Any rejection sends the whole claim back to the employee to fix and resubmit.
            claim.Stage = ApprovalStage.ReturnedToEmployee;
        }
        else
        {
            // Carry each approved amount forward as the amount the next stage will see.
            foreach (var line in claim.Lines) line.CurrentAmount = line.ApprovedAmount!.Value;
            var approvedTotal = claim.Lines.Sum(l => l.CurrentAmount);
            var next = ApprovalStagePolicy.NextStage(actedStage, approvedTotal, _threshold);

            if (next != ApprovalStage.Completed)
            {
                // Re-open the lines for the next stage to decide on the carried amounts.
                foreach (var line in claim.Lines)
                {
                    line.Status = LineDecisionStatus.Pending;
                    line.ApprovedAmount = null;
                }
            }
            claim.Stage = next;
        }

        claim.UpdatedAt = DateTime.UtcNow;
        claim.History.Add(Event(claim, actedStage, ApprovalStagePolicy.RoleLabel(actor.Role), actor.DisplayName,
            $"Approved {approved}, reduced {reduced}, rejected {rejected}."
            + (rejected > 0 ? " Returned to employee." : $" → {ApprovalStagePolicy.StageLabel(claim.Stage)}.")));

        await _repo.SaveChangesAsync(ct);
        return claim;
    }

    /// <summary>Finance records the manual SAP posting reference once a claim is fully approved.</summary>
    public async Task<ClaimRecord> PostAsync(Guid claimId, AuthUser actor, string sapReference, CancellationToken ct = default)
    {
        if (actor.Role != UserRole.Finance)
            throw new WorkflowForbiddenException("Only Finance records the SAP posting.");
        if (string.IsNullOrWhiteSpace(sapReference))
            throw new WorkflowConflictException("A SAP reference number is required to mark a claim posted.");

        var claim = await Require(claimId, ct);
        if (claim.Stage != ApprovalStage.Completed || claim.Posted)
            throw new WorkflowConflictException("Only a completed, not-yet-posted claim can be posted.");

        claim.Posted = true;
        claim.SapReference = sapReference.Trim();
        claim.UpdatedAt = DateTime.UtcNow;
        claim.History.Add(Event(claim, ApprovalStage.Completed, "Finance", actor.DisplayName,
            $"Recorded SAP posting (ref {claim.SapReference})."));

        await _repo.SaveChangesAsync(ct);
        return claim;
    }

    // ---- helpers ----

    private async Task<ClaimRecord> Require(Guid id, CancellationToken ct)
        => await _repo.GetAsync(id, ct) ?? throw new ClaimNotFoundException(id);

    private static void ApplyDecision(ClaimLineRecord line, LineDecisionInput d)
    {
        switch (d.Action?.ToLowerInvariant())
        {
            case "approve":
                line.ApprovedAmount = line.CurrentAmount;
                line.Status = LineDecisionStatus.Approved;
                line.RejectionReason = null;
                break;
            case "reduce":
                if (d.Amount is not { } amt || amt <= 0m || amt >= line.CurrentAmount)
                    throw new WorkflowConflictException("A reduced amount must be greater than zero and less than the current amount.");
                line.ApprovedAmount = amt;
                line.Status = LineDecisionStatus.Reduced;
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

    private static ClaimLineRecord ToLine(SubmitClaimLine input) => new()
    {
        // Leave Id default so EF generates it on insert. (Setting a Guid key on an entity added to
        // an already-tracked claim makes EF treat it as existing → a bogus UPDATE. Idempotency is
        // at the claim level via ClaimId, so server-generated line ids are fine.)
        BeneficiaryKind = input.BeneficiaryKind.ToString(),
        DependantId = input.DependantId,
        DependantRelationship = input.DependantRelationship?.ToString(),
        Category = input.Category.ToString(),
        ExpenseDate = input.ExpenseDate,
        ReceiptReference = input.ReceiptReference,
        ClaimedAmount = input.ClaimedAmount,
        CurrentAmount = input.ClaimedAmount,
        ApprovedAmount = null,
        Status = LineDecisionStatus.Pending,
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
