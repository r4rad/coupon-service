namespace CouponService.Application.Redemption;

public sealed record RedemptionRecord(
    string PartitionKey,
    string OrderId,
    string CustomerId,
    RedemptionState State,
    decimal DiscountApplied,
    string PolicyContentHash,
    string ETag,
    DateTimeOffset? TtlExpiresAt = null);
