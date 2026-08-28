namespace CouponService.Application.Redemption;

public sealed record RedemptionResult(
    bool Succeeded,
    RedemptionRecord? Redemption);
