using Mems.Domain.Common;

namespace Mems.Domain.Entitlement;

/// <summary>
/// A snapshot of an employee's pooled entitlement for one period: the annual cap, how much has
/// already been consumed, and what remains. The pool is shared across the employee and all
/// dependants — there is no per-dependant or per-category sub-pool
/// (docs/entitlement-rules.md § Entitlement definition).
/// </summary>
/// <remarks>
/// "Consumed" is the sum of <em>approved</em> amounts for the period — pending and rejected
/// amounts do not consume entitlement. Computing that sum is a persistence concern (it reads
/// across claims); this value object only reasons about the numbers once they are supplied.
/// </remarks>
public sealed record EntitlementBalance
{
    public Money Cap { get; }
    public Money Consumed { get; }

    public EntitlementBalance(Money cap, Money consumed)
    {
        Cap = cap;
        Consumed = consumed;
    }

    /// <summary>What is left of the cap. Clamped at zero (Money subtraction never goes negative).</summary>
    public Money Remaining => Cap - Consumed;

    /// <summary>
    /// True if consumption has met or exceeded the cap. Distinct from <c>Remaining.IsZero</c>
    /// only conceptually, but named so intent reads clearly at call sites.
    /// </summary>
    public bool IsExhausted => Consumed >= Cap;

    /// <summary>
    /// Whether an additional approved amount would still fit within the remaining cap. Used
    /// when an approver decides how much of a claim line to approve.
    /// </summary>
    public bool CanAccommodate(Money additionalApproved) => additionalApproved <= Remaining;

    /// <summary>
    /// Splits an approved amount into the part covered by this year's remaining entitlement and
    /// the excess, which becomes a Medical Advance Against Next Year's Entitlement
    /// (docs/entitlement-rules.md § Medical Advance from Next Year's Entitlement). The advance is
    /// a tracked amount that reduces next year's opening balance — recording and carrying it is a
    /// persistence concern; this method only computes the decided split.
    /// </summary>
    public ApprovalAllocation Allocate(Money approvedAmount)
    {
        var fromThisYear = approvedAmount <= Remaining ? approvedAmount : Remaining;
        // Money subtraction clamps at zero, so this is 0 whenever the amount fits within remaining.
        var advance = approvedAmount - fromThisYear;
        return new ApprovalAllocation(fromThisYear, advance);
    }
}

/// <summary>
/// How an approved amount is funded: from the current year's remaining entitlement, and — for
/// any excess — as an advance drawn against next year's entitlement.
/// </summary>
public sealed record ApprovalAllocation(Money FromCurrentYear, Money AdvanceAgainstNextYear);
