namespace Mems.Domain.Employees;

/// <summary>
/// The three entitlement bands MEMS covers. Both the annual-entitlement multiplier and the
/// per-line sub-limits (room rate, consultation) key off these exact bands
/// (docs/entitlement-rules.md § Entitlement definition, § Coverage).
/// </summary>
/// <remarks>
/// Scope (docs/entitlement-rules.md § Scope): only Grade M and Grade E employees are covered.
/// Worker/Staff up to Grade S use Social Security and are out of scope for MEMS — there is
/// deliberately no band for them here, so an out-of-scope employee cannot be represented.
/// Eligibility (permanent-only for M-grades; probation/contract allowed for E-grades) is a
/// property of the employee record, not of the band, and is enforced upstream.
/// </remarks>
public enum GradeBand
{
    /// <summary>M4 and above. Annual entitlement: 1× Basic salary.</summary>
    MSenior = 1,

    /// <summary>M1–M3. Annual entitlement: 1.5× Basic salary.</summary>
    MStandard = 2,

    /// <summary>E1–E5. Annual entitlement: 2× Basic salary.</summary>
    Executive = 3,
}
