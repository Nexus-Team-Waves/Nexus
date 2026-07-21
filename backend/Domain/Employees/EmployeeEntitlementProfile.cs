using Mems.Domain.Common;

namespace Mems.Domain.Employees;

/// <summary>
/// The subset of an employee's master data that drives entitlement. Sourced from the
/// Attendance/Payroll DB, which is the authoritative source for employee master data
/// (docs/entitlement-rules.md § Scope; docs/CLAUDE.md §10). MEMS treats this as read-only
/// reference data — it does not own or edit it.
/// </summary>
/// <param name="EmployeeId">Stable identifier from the payroll system.</param>
/// <param name="Band">Entitlement band (drives both the multiplier and the sub-limits).</param>
/// <param name="BasicMonthlySalary">
/// The employee's <em>monthly</em> Basic salary — the base the entitlement factor applies to
/// (docs/entitlement-rules.md § Salary &amp; entitlement base: annual entitlement =
/// Basic Monthly Salary × factor). For M-grade employees this value is derived from the
/// admin-entered Entitled Salary via <see cref="SalaryPolicy.MonthlyBasicFromEntitledSalary"/>;
/// for non-M grades it is the Excel-uploaded monthly figure used as-is.
/// </param>
/// <param name="JoinDate">Employment start date — drives mid-year-joiner pro-rating.</param>
public sealed record EmployeeEntitlementProfile(
    string EmployeeId,
    GradeBand Band,
    Money BasicMonthlySalary,
    DateOnly JoinDate);
