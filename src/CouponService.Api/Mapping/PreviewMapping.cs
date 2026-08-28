using System.Collections.Immutable;
using System.Globalization;
using CouponService.Api.Contracts.Preview;
using CouponService.Application.Preview;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Api.Mapping;

internal static class PreviewMapping
{
    internal static Cart ToCart(CartRequest request) =>
        new(request.Lines
            .Select(line => new CartLine(
                line.LineId,
                line.PizzaId,
                line.Category,
                line.UnitPrice,
                line.Quantity))
            .ToImmutableArray());

    internal static CustomerContext ToCustomer(PreviewRequest request) =>
        new(request.CustomerId, request.ConfirmedOrderCount);

    internal static PreviewResponse ToResponse(PreviewResult result, string currency)
    {
        var breakdown = result.Breakdown;
        return new PreviewResponse(
            result.Decision.Status,
            result.Decision.Reason,
            ToHintResponse(result.Decision.Hint),
            new PricingResponse(
                currency,
                breakdown.Lines
                    .Select(line => new LinePricingResponse(line.LineId, line.Amount))
                    .ToArray(),
                breakdown.Subtotal,
                breakdown.Discount,
                breakdown.Total),
            result.Decision.PolicyContentHash);
    }

    private static NearMissHintResponse? ToHintResponse(Application.Pricing.NearMissHint? hint)
    {
        if (hint is null)
        {
            return null;
        }

        var shortfall = hint.Shortfall;
        var formatted = shortfall.ToString("F2", CultureInfo.InvariantCulture);
        return new NearMissHintResponse(shortfall, $"Spend {formatted} more to use this offer");
    }
}
