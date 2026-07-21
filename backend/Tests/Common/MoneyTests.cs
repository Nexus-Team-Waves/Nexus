using Mems.Domain.Common;

namespace Mems.Tests.Common;

public sealed class MoneyTests
{
    [Fact]
    public void Money_cannot_be_negative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromRupees(-1m));
    }

    [Fact]
    public void Subtraction_is_clamped_at_zero()
    {
        var result = Money.FromRupees(100m) - Money.FromRupees(150m);
        Assert.Equal(0m, result.Amount);
    }

    [Theory]
    [InlineData(100_821.9178, 100_822)]
    [InlineData(100_821.4, 100_821)]
    [InlineData(100_821.5, 100_822)] // .5 rounds away from zero
    public void RoundToNearestRupee_rounds_to_whole_rupees(decimal input, decimal expected)
    {
        Assert.Equal(expected, Money.FromRupees(input).RoundToNearestRupee().Amount);
    }
}
