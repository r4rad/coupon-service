using CouponService.Domain;

namespace CouponService.UnitTests.Domain;

public sealed class PriceBreakdownTests
{
    [Fact]
    public void FromCart_applies_architecture_worked_example_totals()
    {
        var cart = new Cart(
        [
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "specialty", 12.00m, 1),
        ]);

        var breakdown = PriceBreakdown.FromCart("EUR", cart, Money.Percentage(cart.Subtotal, 10m));

        Assert.Equal("EUR", breakdown.Currency);
        Assert.Equal(31.00m, breakdown.Subtotal);
        Assert.Equal(3.10m, breakdown.Discount);
        Assert.Equal(27.90m, breakdown.Total);
    }

    [Fact]
    public void FromCart_caps_discount_at_subtotal_so_total_is_never_negative()
    {
        var cart = new Cart(
        [
            new CartLine("line-1", "margherita", "classic", 10.00m, 1),
        ]);

        var breakdown = PriceBreakdown.FromCart("EUR", cart, 15.00m);

        Assert.Equal(10.00m, breakdown.Subtotal);
        Assert.Equal(10.00m, breakdown.Discount);
        Assert.Equal(0.00m, breakdown.Total);
    }
}
