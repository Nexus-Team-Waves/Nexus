using Mems.Application.Entitlement;
using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Tests.Entitlement;

public sealed class EntitlementServiceTests
{
    // Minimal hand-written fakes — no mocking library needed for two one-method ports.
    private sealed class FakeProfiles(EmployeeEntitlementProfile? profile) : IEmployeeEntitlementProfileProvider
    {
        public Task<EmployeeEntitlementProfile?> GetProfileAsync(string employeeId, CancellationToken ct = default)
            => Task.FromResult(profile);
    }

    private sealed class FakeLedger(decimal consumed) : IEntitlementLedger
    {
        public Task<Money> GetConsumedForYearAsync(string employeeId, int year, CancellationToken ct = default)
            => Task.FromResult(Money.FromRupees(consumed));
    }

    [Fact]
    public async Task GetBalance_combines_prorated_cap_with_consumed_amount()
    {
        var profile = new EmployeeEntitlementProfile("E-001", GradeBand.Executive, Money.FromRupees(100_000), new DateOnly(2020, 1, 1));
        var service = new EntitlementService(new FakeProfiles(profile), new FakeLedger(50_000m));

        var balance = await service.GetBalanceAsync("E-001", 2026);

        Assert.Equal(200_000m, balance.Cap.Amount);      // full year, 2× basic
        Assert.Equal(50_000m, balance.Consumed.Amount);
        Assert.Equal(150_000m, balance.Remaining.Amount);
    }

    [Fact]
    public async Task Unknown_employee_raises_EmployeeNotEntitled()
    {
        var service = new EntitlementService(new FakeProfiles(null), new FakeLedger(0m));
        await Assert.ThrowsAsync<EmployeeNotEntitledException>(() => service.GetBalanceAsync("ghost", 2026));
    }
}
