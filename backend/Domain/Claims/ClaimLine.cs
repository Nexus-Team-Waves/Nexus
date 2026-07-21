using Mems.Domain.Common;

namespace Mems.Domain.Claims;

/// <summary>
/// One line of a claim — a single expense for one beneficiary in one category
/// (docs/entitlement-rules.md § Claim structure). A claim is a header with one or more of these.
/// </summary>
/// <remarks>
/// The status transitions here model the <em>current decision</em> on a line (Pending →
/// Approved / Partially approved / Rejected). The multi-stage routing that produces those
/// decisions (Line Manager → Admin/HR → Finance, plus the value-threshold extra step) is an
/// approval-workflow concern layered on top later — parts of it are still OPEN in the policy
/// (the threshold figure, the resubmission re-entry point), so it is intentionally not encoded
/// here. What <em>is</em> decided — the line states and how they roll up — lives in this type
/// and in <see cref="Claim"/>.
/// </remarks>
public sealed class ClaimLine
{
    public Guid Id { get; }
    public Beneficiary Beneficiary { get; }
    public ClaimCategory Category { get; }

    /// <summary>
    /// When the expense/treatment was incurred. Drives the informational days-elapsed indicator
    /// shown to approvers (docs/entitlement-rules.md § Claim submission timing) and is the as-of
    /// date for judging dependant coverage. Note: lateness never auto-rejects — the approver
    /// decides — so this is not gated as a submission deadline here.
    /// </summary>
    public DateOnly ExpenseDate { get; }

    /// <summary>What the employee entered for this line. Immutable once submitted.</summary>
    public Money ClaimedAmount { get; }

    /// <summary>Optional pointer to the receipt image/file. The bytes live outside the domain.</summary>
    public string? ReceiptReference { get; }

    /// <summary>Null until a stage acts on this line; set on full or partial approval, zeroed on rejection.</summary>
    public Money? ApprovedAmount { get; private set; }

    public ClaimLineStatus Status { get; private set; }

    /// <summary>Required text when <see cref="Status"/> is Rejected; null otherwise.</summary>
    public string? RejectionReason { get; private set; }

    public ClaimLine(
        Guid id,
        Beneficiary beneficiary,
        ClaimCategory category,
        DateOnly expenseDate,
        Money claimedAmount,
        string? receiptReference = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Claim line needs a non-empty id.", nameof(id));
        if (claimedAmount.IsZero)
            throw new ArgumentException("Claimed amount must be greater than zero.", nameof(claimedAmount));

        Id = id;
        Beneficiary = beneficiary ?? throw new ArgumentNullException(nameof(beneficiary));
        Category = category;
        ExpenseDate = expenseDate;
        ClaimedAmount = claimedAmount;
        ReceiptReference = receiptReference;
        Status = ClaimLineStatus.Pending;
        ApprovedAmount = null;
    }

    /// <summary>Approve the full claimed amount.</summary>
    public void ApproveInFull()
    {
        EnsurePending();
        ApprovedAmount = ClaimedAmount;
        Status = ClaimLineStatus.Approved;
        RejectionReason = null;
    }

    /// <summary>
    /// Approve a reduced amount. Must be greater than zero and strictly less than the claimed
    /// amount — equal means "approve in full", greater is never valid.
    /// </summary>
    public void ApprovePartial(Money approvedAmount)
    {
        EnsurePending();
        if (approvedAmount.IsZero)
            throw new ArgumentException("A partial approval must be greater than zero — use Reject instead.", nameof(approvedAmount));
        if (approvedAmount >= ClaimedAmount)
            throw new ArgumentException("A partial approval must be less than the claimed amount — use ApproveInFull.", nameof(approvedAmount));

        ApprovedAmount = approvedAmount;
        Status = ClaimLineStatus.PartiallyApproved;
        RejectionReason = null;
    }

    /// <summary>
    /// Reject the line with a reason. The reason is mandatory: the employee needs it to decide
    /// whether to fix and resubmit (docs/entitlement-rules.md § Claim structure).
    /// </summary>
    public void Reject(string reason)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection must include a reason.", nameof(reason));

        // A rejected line contributes nothing to Total approved; recorded as zero, not null,
        // to distinguish "decided: nothing" from "not yet decided".
        ApprovedAmount = Money.Zero;
        Status = ClaimLineStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    private void EnsurePending()
    {
        if (Status != ClaimLineStatus.Pending)
            throw new InvalidOperationException($"Line {Id} has already been decided ({Status}) and cannot be acted on again.");
    }
}
