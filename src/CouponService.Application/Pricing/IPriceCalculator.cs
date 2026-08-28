using CouponService.Domain;

namespace CouponService.Application.Pricing;

public interface IPriceCalculator
{
    PriceBreakdown Calculate(Cart cart, PolicyDecision decision);
}
