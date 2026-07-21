namespace Mems.Domain.Claims;

/// <summary>
/// The overall status of a claim. This is <em>derived</em> from the claim's line statuses and
/// is never stored independently (docs/entitlement-rules.md § Claim structure — Overall claim
/// status). See <see cref="Claim.OverallStatus"/> for the derivation and its precedence.
/// </summary>
public enum ClaimStatus
{
    /// <summary>At least one line is still Pending and no line is Rejected.</summary>
    InReview = 1,

    /// <summary>Every line is fully Approved.</summary>
    Approved = 2,

    /// <summary>No line is Pending or Rejected, but not all are fully Approved (some partial).</summary>
    PartiallyApproved = 3,

    /// <summary>At least one line is Rejected — the employee must act (fix &amp; resubmit, or accept).</summary>
    ActionNeeded = 4,
}
