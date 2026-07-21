using Mems.Domain.Employees;

namespace Mems.Tests.Employees;

public sealed class DependantCoverageTests
{
    private static readonly DateOnly AsOf = new(2026, 7, 21);

    private static Dependant Child(int ageYears, bool student = false, bool married = false)
        => new(DependantRelationship.Child, AsOf.AddYears(-ageYears), married, student);

    [Fact]
    public void A_spouse_is_always_covered()
    {
        var spouse = new Dependant(DependantRelationship.Spouse, new DateOnly(1990, 1, 1), IsMarried: true, IsFullTimeStudent: false);
        Assert.True(spouse.IsCoveredAsOf(AsOf));
    }

    [Fact]
    public void A_child_up_to_19_is_covered_without_being_a_student()
    {
        Assert.True(Child(19).IsCoveredAsOf(AsOf));
        Assert.True(Child(10).IsCoveredAsOf(AsOf));
    }

    [Fact]
    public void A_child_over_19_is_not_covered_unless_an_unmarried_full_time_student()
    {
        Assert.False(Child(20).IsCoveredAsOf(AsOf));                         // 20, not a student
        Assert.True(Child(20, student: true).IsCoveredAsOf(AsOf));           // 20, student
        Assert.True(Child(23, student: true).IsCoveredAsOf(AsOf));           // 23, student — at the limit
    }

    [Fact]
    public void The_student_extension_stops_at_23_and_excludes_married_children()
    {
        Assert.False(Child(24, student: true).IsCoveredAsOf(AsOf));               // over 23
        Assert.False(Child(21, student: true, married: true).IsCoveredAsOf(AsOf)); // married student
    }
}
