namespace CouponService.Application.Policies;

public sealed record PolicyRecord(
    string PartitionKey,
    string PolicyId,
    string? Code,
    PolicyTrigger Trigger,
    string DocumentJson,
    string ETag);
