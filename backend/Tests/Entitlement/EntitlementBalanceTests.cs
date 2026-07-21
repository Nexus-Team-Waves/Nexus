using Mems.Domain.Common;
using Mems.Domain.Entitlement;

namespace Mems.Tests.Entitlement;

public sealed class EntitlementBalanceTests
{
    [Fact]
    public void Remaining_is_cap_minus_consumed()
    {
        var balance = new EntitlementBalance(Money.FromRupees(200_000), Money.FromRupees(60_000));
        Assert.Equal(140_000m, balance.Remaining.Amount);
        Assert.False(balance.IsExhausted);
    }

    [Fact]
    public void Over_consumption_clamps_remaining_at_zero_and_reports_exhausted()
    {
        var balance = new EntitlementBalance(Money.FromRupees(200_000), Money.FromRupees(250_000));
        Assert.Equal(0m, balance.Remaining.Amount);
        Assert.True(balance.IsExhausted);
    }

    [Fact]
    public void CanAccommodate_is_true_only_within_remaining()
    {
        var balance = new EntitlementBalance(Money.FromRupees(200_000), Money.FromRupees(180_000));
        Assert.True(balance.CanAccommodate(Money.FromRupees(20_000)));
        Assert.False(balance.CanAccommodate(Money.FromRupees(20_001)));
    }

    [Fact]
    public void Allocate_splits_the_excess_over_remaining_into_an_advance()
    {
        // Worked example from the policy doc: 2026 cap 300k, consumed 260k → remaining 40k.
        var balance = new EntitlementBalance(Money.FromRupees(300_000), Money.FromRupees(260_000));

        var alloc = balance.Allocate(Money.FromRupees(60_000));
        Assert.Equal(40_000m, alloc.FromCurrentYear.Amount);
        Assert.Equal(20_000m, alloc.AdvanceAgainstNextYear.Amount);
    }

    [Fact]
    public void Allocate_takes_no_advance_when_the_amount_fits_within_remaining()
    {
        var balance = new EntitlementBalance(Money.FromRupees(300_000), Money.FromRupees(260_000));

        var alloc = balance.Allocate(Money.FromRupees(30_000));
        Assert.Equal(30_000m, alloc.FromCurrentYear.Amount);
        Assert.Equal(0m, alloc.AdvanceAgainstNextYear.Amount);
    }
}
