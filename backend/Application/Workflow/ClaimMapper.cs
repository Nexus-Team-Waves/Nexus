namespace Mems.Application.Workflow;

/// <summary>Maps the persisted <see cref="ClaimRecord"/> to the API's <see cref="ClaimDto"/>.</summary>
public static class ClaimMapper
{
    public static ClaimOverallStatus OverallStatus(ClaimRecord c)
    {
        if (c.Posted) return ClaimOverallStatus.Posted;
        if (c.Stage == ApprovalStage.Closed) return ClaimOverallStatus.Rejected;
        if (c.Stage == ApprovalStage.ReturnedToEmployee) return ClaimOverallStatus.ActionNeeded;
        if (c.Stage == ApprovalStage.Completed)
        {
            // Approved outright only if every line's final amount equals what was claimed.
            var full = c.Lines.All(l => l.ApprovedAmount.HasValue && l.ApprovedAmount.Value == l.ClaimedAmount);
            return full ? ClaimOverallStatus.Approved : ClaimOverallStatus.PartiallyApproved;
        }
        return ClaimOverallStatus.InReview;
    }

    /// <summary>
    /// Editable by the employee when it is still entirely un-acted-on (at Line Manager with no
    /// history), or when it was returned after a rejection (docs/entitlement-rules.md § Editing rules).
    /// </summary>
    public static bool Editable(ClaimRecord c)
        => c.Stage == ApprovalStage.ReturnedToEmployee
           || (c.Stage == ApprovalStage.LineManager && c.History.Count == 0);

    public static decimal TotalClaimed(ClaimRecord c) => c.Lines.Sum(l => l.ClaimedAmount);

    /// <summary>Sum of decided amounts. Meaningful once completed; 0 for a freshly submitted claim.</summary>
    public static decimal TotalApproved(ClaimRecord c) => c.Lines.Sum(l => l.ApprovedAmount ?? 0m);

    public static ClaimDto ToDto(ClaimRecord c)
    {
        var eventsByLine = c.LineEvents
            .OrderBy(e => e.At)
            .GroupBy(e => e.LineId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ClaimLineEventDto>)g.Select(e => new ClaimLineEventDto(
                    ApprovalStagePolicy.StageLabel(e.Stage), e.Action, e.ActorRole, e.ActorName,
                    e.Amount, e.Reason, e.Comment, e.At)).ToList());

        var lines = c.Lines
            .OrderBy(l => l.ExpenseDate)
            .Select(l => new ClaimLineDto(
                l.Id, l.BeneficiaryKind, l.DependantId, l.DependantRelationship, l.Category,
                l.ExpenseDate, l.ClaimedAmount, l.CurrentAmount, l.ApprovedAmount,
                l.Status.ToString(), l.RejectionReason, l.ReceiptReference, l.ReceiptIds,
                // Locked = decided in an earlier round and never re-opened (chain-active lines
                // are always reset to Pending when a stage advances).
                l.Status is LineDecisionStatus.Approved or LineDecisionStatus.Reduced,
                eventsByLine.GetValueOrDefault(l.Id) ?? Array.Empty<ClaimLineEventDto>()))
            .ToList();

        var history = c.History
            .OrderBy(h => h.At)
            .Select(h => new ApprovalEventDto(
                ApprovalStagePolicy.StageLabel(h.Stage), h.ActorRole, h.ActorName, h.Summary, h.At))
            .ToList();

        return new ClaimDto(
            c.Id, c.EmployeeId, c.SubmissionDate,
            c.Stage.ToString(), ApprovalStagePolicy.StageLabel(c.Stage),
            OverallStatus(c).ToString(), c.Posted, c.SapReference,
            TotalClaimed(c), TotalApproved(c), Editable(c), lines, history);
    }
}
