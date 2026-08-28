namespace CouponService.Application.Redemption;

internal static class RedemptionConstants
{
    internal static readonly TimeSpan ReservationTtl = TimeSpan.FromSeconds(900);

    internal const int MaxRetryAttempts = 3;
}
