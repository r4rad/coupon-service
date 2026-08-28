using CouponService.Domain;

namespace CouponService.Application.Pricing;

public sealed record PolicyDecision(CouponStatus Status, DiscountPlan? Plan);
