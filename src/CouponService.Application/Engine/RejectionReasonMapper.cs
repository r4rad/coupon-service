using CouponService.Application.Pricing;
using CouponService.Engine.Evaluation;

namespace CouponService.Application.Engine;

internal static class RejectionReasonMapper
{
    internal static RejectionReason FromNearMiss(NearMissRecord nearMiss) =>
        nearMiss.Path.Contains("cart.subtotal", StringComparison.Ordinal)
            ? RejectionReason.MinimumOrderNotMet
            : nearMiss.Path.Contains("line.category", StringComparison.Ordinal)
                ? RejectionReason.CategoryNotEligible
                : nearMiss.Path.Contains("customer.confirmedOrderCount", StringComparison.Ordinal)
                    || nearMiss.Path.Contains("customer.isFirstOrder", StringComparison.Ordinal)
                    ? RejectionReason.NotFirstOrder
                    : nearMiss.Path.Contains("time.localDayOfWeek", StringComparison.Ordinal)
                        || nearMiss.Path.Contains("time.localHour", StringComparison.Ordinal)
                        ? RejectionReason.DayNotEligible
                        : nearMiss.Path.Contains("coupon.uses.total", StringComparison.Ordinal)
                            ? RejectionReason.UsageLimitReached
                            : nearMiss.Path.Contains("coupon.uses.byCustomer", StringComparison.Ordinal)
                                ? RejectionReason.PerCustomerLimitReached
                                : RejectionReason.MinimumOrderNotMet;

    internal static NearMissHint? ToHint(IReadOnlyList<NearMissRecord> nearMisses)
    {
        if (nearMisses.Count == 0)
        {
            return null;
        }

        var best = nearMisses
            .OrderByDescending(nearMiss => nearMiss.Shortfall)
            .First();

        return new NearMissHint(best.Shortfall);
    }
}
