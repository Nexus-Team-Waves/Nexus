namespace Mems.Domain.Employees;

/// <summary>
/// A dependant of an employee. Dependants share the employee's single pooled entitlement —
/// they do not have a separate pool (docs/entitlement-rules.md § Entitlement definition).
/// This type answers one question: is this dependant currently <em>covered</em>?
/// </summary>
/// <param name="Relationship">Spouse or child — the only covered relationships.</param>
/// <param name="DateOfBirth">Used to compute the child age limits. Ignored for a spouse.</param>
/// <param name="IsMarried">A married child is not covered under the student extension.</param>
/// <param name="IsFullTimeStudent">Extends child cover from age 19 to 23 while true.</param>
public sealed record Dependant(
    DependantRelationship Relationship,
    DateOnly DateOfBirth,
    bool IsMarried,
    bool IsFullTimeStudent)
{
    // Child cover cut-offs, straight from policy (docs/entitlement-rules.md § Coverage).
    private const int ChildAgeLimit = 19;
    private const int StudentChildAgeLimit = 23;

    /// <summary>
    /// Whether this dependant is covered as at <paramref name="asOf"/>. A spouse is always
    /// covered; a child is covered up to 19, or up to 23 while unmarried and in full-time
    /// study. The as-of date is passed in (never read from the clock here) so the rule is
    /// pure and testable, and so a claim can be judged against the date of treatment.
    /// </summary>
    public bool IsCoveredAsOf(DateOnly asOf)
    {
        if (Relationship == DependantRelationship.Spouse)
            return true;

        var age = AgeInYearsAt(asOf);
        if (age <= ChildAgeLimit)
            return true;

        return age <= StudentChildAgeLimit && IsFullTimeStudent && !IsMarried;
    }

    /// <summary>Completed years of age at the given date.</summary>
    private int AgeInYearsAt(DateOnly asOf)
    {
        var age = asOf.Year - DateOfBirth.Year;
        // Subtract a year if the birthday has not yet occurred in the as-of year.
        if (asOf < DateOfBirth.AddYears(age))
            age--;
        return age;
    }
}
