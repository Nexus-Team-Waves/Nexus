using Mems.Domain.Common;

namespace Mems.Domain.Employees;

/// <summary>
/// How a Basic Monthly Salary is arrived at, per grade. MEMS never uses payroll's monthly
/// salary feed directly (docs/entitlement-rules.md § Salary &amp; entitlement base):
/// <list type="bullet">
///   <item><b>M grades</b> — an Application Administrator enters an <em>Entitled Salary</em>,
///     from which Basic Monthly Salary = Entitled Salary × 2/3.</item>
///   <item><b>Non-M grades</b> — Basic Monthly Salary is uploaded via Excel and used as-is
///     (no 2/3 conversion).</item>
/// </list>
/// Both feed the same annual-entitlement calculation (× the grade factor).
/// </summary>
public static class SalaryPolicy
{
    /// <summary>
    /// M-grade conversion: <c>Basic Monthly Salary = Entitled Salary × (2 / 3)</c>. Transcribed
    /// verbatim from policy — MEMS does not editorialise the ratio. The result is intentionally
    /// left un-rounded (the DECIMAL(18,4) store and the final Rupee rounding handle the repeating
    /// fraction; see docs/entitlement-rules.md § Money).
    /// </summary>
    public static Money MonthlyBasicFromEntitledSalary(Money entitledSalary)
        => Money.FromRupees(entitledSalary.Amount * 2m / 3m);
}
