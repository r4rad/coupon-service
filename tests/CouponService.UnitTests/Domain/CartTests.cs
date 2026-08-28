using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.UnitTests.Domain;

public sealed class CartTests
{
    [Fact]
    public void Subtotal_sums_rounded_line_totals_for_architecture_worked_example()
    {
        var cart = new Cart(
        [
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "specialty", 12.00m, 1),
        ]);

        Assert.Equal(19.00m, Money.LineTotal(9.50m, 2));
        Assert.Equal(12.00m, Money.LineTotal(12.00m, 1));
        Assert.Equal(31.00m, cart.Subtotal);
    }
}
