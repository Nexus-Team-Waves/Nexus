using Mems.Domain.Employees;

namespace Mems.Application.Entitlement;

/// <summary>
/// Port for reading an employee's entitlement profile from the authoritative source
/// (the Attendance/Payroll DB — docs/entitlement-rules.md § Scope). The concrete adapter lives
/// in Infrastructure (out of scope for this pass); the Application layer depends only on this
/// abstraction so it can be mocked and swapped (CLAUDE.md §6).
/// </summary>
public interface IEmployeeEntitlementProfileProvider
{
    /// <summary>Returns the profile, or null if the employee is unknown / out of MEMS scope.</summary>
    Task<EmployeeEntitlementProfile?> GetProfileAsync(string employeeId, CancellationToken cancellationToken = default);
}
