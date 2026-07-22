using Microsoft.EntityFrameworkCore;
using Mems.Application.Auth;
using Mems.Application.Entitlement;
using Mems.Application.Workflow;
using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Infrastructure.Persistence;

/// <summary>Serves the seeded demo employee profile (stands in for the Attendance/Payroll feed).</summary>
public sealed class DemoEmployeeProfileProvider : IEmployeeEntitlementProfileProvider
{
    public Task<EmployeeEntitlementProfile?> GetProfileAsync(string employeeId, CancellationToken cancellationToken = default)
        => Task.FromResult(DemoDirectory.Employees.TryGetValue(employeeId, out var e) ? e.Profile : null);
}

/// <summary>
/// Consumed entitlement = the sum of approved amounts on the employee's fully-decided (Completed or
/// Posted) claims for the year. Pending/in-progress claims do not consume entitlement
/// (docs/entitlement-rules.md § Entitlement definition).
/// </summary>
public sealed class ClaimEntitlementLedger(MemsDbContext db) : IEntitlementLedger
{
    public async Task<Money> GetConsumedForYearAsync(string employeeId, int year, CancellationToken cancellationToken = default)
    {
        // Filter by employee + fully-decided in the query; the year filter and the decimal sum run in
        // memory so this translates cleanly on any provider (SQLite can't translate DateOnly.Year or
        // aggregate decimals).
        var claims = await db.Claims.Include(c => c.Lines)
            .Where(c => c.EmployeeId == employeeId && (c.Stage == ApprovalStage.Completed || c.Posted))
            .ToListAsync(cancellationToken);

        var total = claims
            .Where(c => c.SubmissionDate.Year == year)
            .SelectMany(c => c.Lines)
            .Sum(l => l.ApprovedAmount ?? 0m);
        return Money.FromRupees(total);
    }
}
