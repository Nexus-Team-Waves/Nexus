using Mems.Domain.Entitlement;

namespace Mems.Application.Entitlement;

/// <summary>
/// Use-case service that answers "what is this employee's entitlement balance for a year?".
/// It orchestrates the two ports (profile + consumption ledger) and the pure domain calculator;
/// it holds no rules of its own — the rules live in <see cref="EntitlementCalculator"/> and
/// <see cref="EntitlementBalance"/>.
/// </summary>
public sealed class EntitlementService
{
    private readonly IEmployeeEntitlementProfileProvider _profiles;
    private readonly IEntitlementLedger _ledger;

    public EntitlementService(IEmployeeEntitlementProfileProvider profiles, IEntitlementLedger ledger)
    {
        _profiles = profiles;
        _ledger = ledger;
    }

    /// <summary>
    /// Computes the pooled entitlement balance for an employee in a calendar year: the
    /// (pro-rated, if a mid-year joiner) annual cap, minus what has already been consumed.
    /// </summary>
    /// <exception cref="EmployeeNotEntitledException">
    /// Thrown when the employee is unknown to the payroll source or is out of MEMS scope.
    /// </exception>
    public async Task<EntitlementBalance> GetBalanceAsync(
        string employeeId, int year, CancellationToken cancellationToken = default)
    {
        var profile = await _profiles.GetProfileAsync(employeeId, cancellationToken)
            ?? throw new EmployeeNotEntitledException(employeeId);

        var cap = EntitlementCalculator.AnnualEntitlementForYear(profile, year);
        var consumed = await _ledger.GetConsumedForYearAsync(employeeId, year, cancellationToken);

        return new EntitlementBalance(cap, consumed);
    }
}

/// <summary>Raised when an entitlement balance is requested for an employee MEMS does not cover.</summary>
public sealed class EmployeeNotEntitledException(string employeeId)
    : Exception($"Employee '{employeeId}' has no MEMS entitlement profile (unknown or out of scope).");
