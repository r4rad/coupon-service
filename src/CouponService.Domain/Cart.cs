using System.Collections.Immutable;

namespace CouponService.Domain;

public sealed record CartLine(
    string LineId,
    string PizzaId,
    string Category,
    decimal UnitPrice,
    int Quantity);

public sealed record Cart(ImmutableArray<CartLine> Lines)
{
    public decimal Subtotal =>
        Lines.Sum(line => Money.LineTotal(line.UnitPrice, line.Quantity));
}
