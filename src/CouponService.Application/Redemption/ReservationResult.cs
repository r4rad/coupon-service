using CouponService.Application.Pricing;

namespace CouponService.Application.Redemption;

public sealed record ReservationResult(
    bool Succeeded,
    RejectionReason? Reason,
    RedemptionRecord? Redemption);
