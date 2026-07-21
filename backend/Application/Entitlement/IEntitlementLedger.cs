using Mems.Domain.Common;

namespace Mems.Application.Entitlement;

/// <summary>
/// Port for reading how much of an employee's pooled entitlement has already been consumed in a
/// given calendar year. "Consumed" is the sum of <em>approved</em> amounts across the pool
/// (employee + dependants) for that year — pending and rejected amounts do not count
/// (docs/entitlement-rules.md § Entitlement definition). The concrete adapter (an EF Core query
/// over the MEMS DB) lives in Infrastructure, out of scope for this pass.
/// </summary>
public interface IEntitlementLedger
{
    /// <summary>Total approved (consumed) amount for the employee's pool in <paramref name="year"/>.</summary>
    Task<Money> GetConsumedForYearAsync(string employeeId, int year, CancellationToken cancellationToken = default);
}
