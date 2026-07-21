using Mems.Domain.Common;
using Mems.Domain.Employees;

namespace Mems.Tests.Employees;

public sealed class SalaryPolicyTests
{
    [Fact]
    public void M_grade_basic_monthly_is_two_thirds_of_the_entitled_salary()
    {
        var basic = SalaryPolicy.MonthlyBasicFromEntitledSalary(Money.FromRupees(1_500_000m));
        Assert.Equal(1_000_000m, basic.Amount); // 1,500,000 × 2/3
    }
}
