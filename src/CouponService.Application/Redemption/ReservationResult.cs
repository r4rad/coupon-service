using CouponService.Application.Pricing;
using CouponService.Domain;

namespace CouponService.Application.Redemption;

public sealed record ReservationResult(
    bool Succeeded,
    RejectionReason? Reason,
    RedemptionRecord? Redemption,
    PriceBreakdown? Breakdown);
