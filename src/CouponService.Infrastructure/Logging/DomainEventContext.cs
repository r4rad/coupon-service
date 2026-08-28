namespace CouponService.Infrastructure.Logging;

public sealed record DomainEventContext(
    string? CouponCode = null,
    string? OrderId = null,
    string? PolicyContentHash = null,
    string? UserId = null);
