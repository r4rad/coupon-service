using Microsoft.Extensions.Logging;

namespace CouponService.Infrastructure.Logging;

public sealed class SerilogDomainEventLogger(ILogger<SerilogDomainEventLogger> logger) : IDomainEventLogger
{
    public void Log(string eventName, DomainEventContext context)
    {
        logger.LogInformation(
            "Domain event {DomainEvent} for coupon {CouponCode} order {OrderId} policy {PolicyContentHash} user {UserId}",
            eventName,
            context.CouponCode,
            context.OrderId,
            context.PolicyContentHash,
            context.UserId);
    }
}
