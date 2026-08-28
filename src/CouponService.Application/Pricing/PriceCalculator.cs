using CouponService.Domain;

namespace CouponService.Application.Pricing;

public sealed class PriceCalculator : IPriceCalculator
{
    public const string DefaultCurrency = "EUR";

    public PriceBreakdown Calculate(Cart cart, PolicyDecision decision) =>
        PriceBreakdown.FromCart(
            DefaultCurrency,
            cart,
            decision.Status is CouponStatus.Applied && decision.Plan is not null
                ? decision.Plan.Total
                : 0m);
}
