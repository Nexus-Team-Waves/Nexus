using Mems.Domain.Common;
using Mems.Domain.Employees;
using Mems.Domain.Entitlement;

namespace Mems.Tests.Entitlement;

public sealed class EntitlementCalculatorTests
{
    private static EmployeeEntitlementProfile Profile(GradeBand band, decimal basic, DateOnly joinDate)
        => new("E-001", band, Money.FromRupees(basic), joinDate);

    [Theory]
    [InlineData(GradeBand.MSenior, 100_000, 100_000)]    // 1.0×  (M4+)
    [InlineData(GradeBand.MStandard, 100_000, 150_000)]  // 1.5×  (M1–M3)
    [InlineData(GradeBand.Executive, 100_000, 200_000)]  // 2.0×  (E1–E5)
    public void FullAnnualEntitlement_applies_the_band_multiplier(GradeBand band, decimal basic, decimal expected)
    {
        var result = EntitlementCalculator.FullAnnualEntitlement(band, Money.FromRupees(basic));
        Assert.Equal(expected, result.Amount);
    }

    [Fact]
    public void Employee_who_joined_in_an_earlier_year_gets_the_full_entitlement()
    {
        var profile = Profile(GradeBand.Executive, 100_000, new DateOnly(2020, 3, 15));
        var result = EntitlementCalculator.AnnualEntitlementForYear(profile, 2026);
        Assert.Equal(200_000m, result.Amount);
    }

    [Fact]
    public void Mid_year_joiner_is_pro_rated_on_days_over_365()
    {
        // Joined 1 Jul 2026 → 184 days (Jul 1..Dec 31). 184/365 × 200,000 = 100,821.92 → 100,822.
        var profile = Profile(GradeBand.Executive, 100_000, new DateOnly(2026, 7, 1));
        var result = EntitlementCalculator.AnnualEntitlementForYear(profile, 2026);
        Assert.Equal(100_822m, result.Amount);
    }

    [Fact]
    public void Employee_who_has_not_yet_joined_by_year_end_gets_zero()
    {
        var profile = Profile(GradeBand.Executive, 100_000, new DateOnly(2027, 1, 1));
        var result = EntitlementCalculator.AnnualEntitlementForYear(profile, 2026);
        Assert.Equal(0m, result.Amount);
    }

    [Fact]
    public void Joining_on_1_January_still_yields_the_full_year()
    {
        // 1 Jan..31 Dec inclusive = 365 days → fraction 1.0.
        var profile = Profile(GradeBand.MStandard, 100_000, new DateOnly(2026, 1, 1));
        var result = EntitlementCalculator.AnnualEntitlementForYear(profile, 2026);
        Assert.Equal(150_000m, result.Amount);
    }

    [Fact]
    public void A_mid_year_increment_is_pro_rated_from_its_effective_date()
    {
        // New basic 120,000 × 2 = 240,000 new annual. Effective 1 Jul 2026 → 184 days.
        // 184/365 × 240,000 = 120,986.30 → 120,986.
        var result = EntitlementCalculator.ProratedEntitlementForIncrement(
            GradeBand.Executive, Money.FromRupees(120_000m), new DateOnly(2026, 7, 1), 2026);
        Assert.Equal(120_986m, result.Amount);
    }
}
