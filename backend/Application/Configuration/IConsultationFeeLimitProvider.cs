using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Application.Configuration;

/// <summary>
/// Port for the admin-configured, per-grade consultation-fee limit
/// (docs/entitlement-rules.md § Coverage). As of the 2026-07-21 rules this limit is NOT a
/// hardcoded policy constant — it is maintained on an admin screen (grade, limit, effective
/// date, active flag) and read at runtime so it can change without a code deploy. It is a
/// <b>soft</b> limit: it is surfaced to approvers alongside the claimed amount and whether the
/// claim exceeds it, but approvers may still approve over-limit claims per their permissions.
/// The concrete adapter lives in Infrastructure (out of scope for this pass).
/// </summary>
public interface IConsultationFeeLimitProvider
{
    /// <summary>
    /// The consultation-fee limit in effect for <paramref name="band"/> as at
    /// <paramref name="asOf"/>, or <c>null</c> if none is configured/active. Callers must treat
    /// <c>null</c> as "no configured limit", never as zero or unlimited.
    /// </summary>
    Task<Money?> GetLimitAsync(GradeBand band, DateOnly asOf, CancellationToken cancellationToken = default);
}
