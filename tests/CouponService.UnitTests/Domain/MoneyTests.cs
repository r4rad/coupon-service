using CouponService.Domain;

namespace CouponService.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(2.005, 2.01)]
    [InlineData(-2.005, -2.01)]
    public void Round_applies_midpoint_away_from_zero(decimal input, decimal expected) =>
        Assert.Equal(expected, Money.Round(input));

    [Fact]
    public void Percentage_on_architecture_worked_example_subtotal_yields_expected_discount()
    {
        const decimal subtotal = 31.00m;

        Assert.Equal(3.10m, Money.Percentage(subtotal, 10m));
    }

    [Fact]
    public void CapDiscount_limits_discount_to_base_so_total_cannot_go_negative()
    {
        const decimal subtotal = 10.00m;
        const decimal uncappedDiscount = 15.00m;

        var cappedDiscount = Money.CapDiscount(uncappedDiscount, subtotal);

        Assert.Equal(10.00m, cappedDiscount);
        Assert.Equal(0.00m, subtotal - cappedDiscount);
    }
}
