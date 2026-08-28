namespace CouponService.Infrastructure.Logging;

public static class DomainEventNames
{
    public const string CouponPreviewed = nameof(CouponPreviewed);

    public const string CouponApplied = nameof(CouponApplied);

    public const string CouponRejected = nameof(CouponRejected);

    public const string ReservationCreated = nameof(ReservationCreated);

    public const string RedemptionConfirmed = nameof(RedemptionConfirmed);

    public const string ReservationReleased = nameof(ReservationReleased);

    public const string ReservationExpired = nameof(ReservationExpired);

    public const string UsageLimitReached = nameof(UsageLimitReached);

    public const string CouponServiceUnavailable = nameof(CouponServiceUnavailable);
}
