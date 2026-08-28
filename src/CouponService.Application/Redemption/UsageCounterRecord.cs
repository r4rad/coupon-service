namespace CouponService.Application.Redemption;

public sealed record UsageCounterRecord(
    string PartitionKey,
    int ConfirmedCount,
    int ActiveReservations,
    string ETag);
