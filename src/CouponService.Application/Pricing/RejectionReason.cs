namespace CouponService.Application.Pricing;

public enum RejectionReason
{
    NotFound,
    Expired,
    NotYetActive,
    MinimumOrderNotMet,
    CategoryNotEligible,
    UsageLimitReached,
    PerCustomerLimitReached,
    NotFirstOrder,
    DayNotEligible,
    Disabled,
}
