using CouponService.Application.Pricing;
using CouponService.Domain;

namespace CouponService.Application.Preview;

public sealed record PreviewResult(PolicyDecision Decision, PriceBreakdown Breakdown);
