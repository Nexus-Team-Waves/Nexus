using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Domain.Entitlement;

/// <summary>
/// Computes an employee's annual medical entitlement. Pure and stateless: every input
/// (including the year and any reference date) is passed in, so results are deterministic and
/// unit-testable, and nothing here reads the system clock.
/// </summary>
/// <remarks>
/// Rules encoded (docs/entitlement-rules.md § Salary &amp; entitlement base):
/// <list type="bullet">
///   <item>Annual entitlement = <c>factor(band) × Basic Monthly Salary</c>.</item>
///   <item>Period is the calendar year; unused balance lapses at year-end (no carry-over).</item>
///   <item>Three proration scenarios share one daily formula, <c>(days ÷ 365) × annual</c>:
///     mid-year <b>joiner</b> (from join date), and mid-year <b>salary increment</b>
///     (from effective date). Mid-year grade change is handled by recomputing at the new grade.</item>
///   <item>The result is a single pooled cap shared by the employee and all dependants.</item>
/// </list>
/// </remarks>
public static class EntitlementCalculator
{
    /// <summary>
    /// The full (un-prorated) annual entitlement for a band and Basic Monthly Salary. This is
    /// the figure a full-year employee at that salary receives.
    /// </summary>
    public static Money FullAnnualEntitlement(GradeBand band, Money basicMonthlySalary)
        => (basicMonthlySalary * EntitlementRule.AnnualMultiplier(band)).RoundToNearestRupee();

    /// <summary>
    /// The entitlement for a specific calendar <paramref name="year"/>, pro-rated if the
    /// employee joined part-way through it. Someone who joined in an earlier year gets the full
    /// entitlement; someone who has not yet joined by year-end gets zero.
    /// </summary>
    public static Money AnnualEntitlementForYear(EmployeeEntitlementProfile profile, int year)
    {
        var full = FullAnnualEntitlement(profile.Band, profile.BasicMonthlySalary);
        return ProrateFromDate(full, profile.JoinDate, year);
    }

    /// <summary>
    /// The prorated entitlement that a mid-year salary increment grants for the remainder of the
    /// year: <c>(days from effective date to year-end ÷ 365) × new full annual</c>
    /// (docs/entitlement-rules.md § Salary increments). This is the revised entitlement for the
    /// remaining period only — entitlement already consumed before the increment is preserved by
    /// the caller (the increment does not apply retroactively).
    /// </summary>
    public static Money ProratedEntitlementForIncrement(
        GradeBand band, Money newBasicMonthlySalary, DateOnly effectiveDate, int year)
    {
        var full = FullAnnualEntitlement(band, newBasicMonthlySalary);
        return ProrateFromDate(full, effectiveDate, year);
    }

    /// <summary>
    /// Shared daily-basis proration: the fraction of <paramref name="fullAnnual"/> earned from
    /// <paramref name="fromDate"/> through 31 Dec of <paramref name="year"/>, inclusive, over a
    /// fixed 365-day denominator (per policy — used verbatim even in leap years). A date before
    /// the year yields the full amount; a date after the year yields zero.
    /// </summary>
    private static Money ProrateFromDate(Money fullAnnual, DateOnly fromDate, int year)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        if (fromDate > yearEnd)
            return Money.Zero;

        var start = fromDate < yearStart ? yearStart : fromDate;
        var days = yearEnd.DayNumber - start.DayNumber + 1;

        var fraction = (decimal)days / EntitlementRule.ProRataDaysInYear;
        return (fullAnnual * fraction).RoundToNearestRupee();
    }
}
