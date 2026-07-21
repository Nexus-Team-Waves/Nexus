namespace Mems.Domain.Claims;

/// <summary>
/// The status of a single claim line. Approval is per line, not per claim — the lines of one
/// claim can end up in different statuses after the same approval stage acts
/// (docs/entitlement-rules.md § Claim structure, § Approval flow).
/// </summary>
public enum ClaimLineStatus
{
    /// <summary>Awaiting a decision from the current approval stage. Approved amount is unset.</summary>
    Pending = 1,

    /// <summary>Approved for the full claimed amount.</summary>
    Approved = 2,

    /// <summary>Approved for less than the claimed amount (approver reduced it).</summary>
    PartiallyApproved = 3,

    /// <summary>Rejected with a reason. The employee may edit and resubmit (see editing rules).</summary>
    Rejected = 4,
}
