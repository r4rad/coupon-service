using System.Collections.Immutable;

namespace CouponService.Domain;

public sealed record LineTotal(string LineId, decimal Amount);

public sealed record PriceBreakdown(
    string Currency,
    ImmutableArray<LineTotal> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total)
{
    public static PriceBreakdown FromCart(string currency, Cart cart, decimal discountAmount)
    {
        var lines = cart.Lines
            .Select(line => new LineTotal(
                line.LineId,
                Money.LineTotal(line.UnitPrice, line.Quantity)))
            .ToImmutableArray();

        var subtotal = lines.Sum(line => line.Amount);
        var discount = Money.CapDiscount(discountAmount, subtotal);
        var total = Money.Round(subtotal - discount);

        return new PriceBreakdown(currency, lines, subtotal, discount, total);
    }
}

public sealed record LineAllocation(string LineId, decimal Amount);

public sealed record DiscountPlan(decimal Total, ImmutableArray<LineAllocation> Allocations);
