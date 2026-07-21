namespace Mems.Domain.Common;

/// <summary>
/// A monetary amount in Pakistani Rupees (PKR). MEMS is single-currency
/// (docs/entitlement-rules.md § Money), so there is deliberately no currency field —
/// adding one would imply a multi-currency capability the policy does not grant.
/// </summary>
/// <remarks>
/// Stored as <see cref="decimal"/> to mirror the MEMS DB column type <c>DECIMAL(18,4)</c>
/// (CLAUDE.md §8). Never use <c>double</c>/<c>float</c> for money — binary floating point
/// cannot represent Rupee amounts exactly and would drift at the cap boundary.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    /// <summary>The raw amount, in Rupees, carrying up to 4 decimal places.</summary>
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static readonly Money Zero = new(0m);

    /// <summary>Create a non-negative amount. Claims, caps and salaries are never negative.</summary>
    public static Money FromRupees(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Money cannot be negative.");
        return new Money(amount);
    }

    /// <summary>
    /// Round to the nearest whole Rupee. The policy caps and figures are all whole Rupees, and
    /// docs/entitlement-rules.md § Money states the cap-boundary rule as "round to nearest".
    /// Used on computed results (pro-rata entitlement, cap boundaries) — not on user-entered
    /// receipt amounts, which keep their as-entered precision until a cap is applied.
    /// </summary>
    public Money RoundToNearestRupee() => new(Math.Round(Amount, 0, MidpointRounding.AwayFromZero));

    public bool IsZero => Amount == 0m;

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    /// <summary>
    /// Subtraction is clamped at zero: a remaining-balance calculation must never surface a
    /// negative entitlement to the caller. Over-consumption is expressed as <see cref="Zero"/>
    /// remaining, and detected separately by comparing consumed against the cap.
    /// </summary>
    public static Money operator -(Money a, Money b) => new(Math.Max(0m, a.Amount - b.Amount));

    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;

    public override string ToString() => $"PKR {Amount:N2}";
}
