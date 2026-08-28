namespace CouponService.Infrastructure.Logging;

public interface IDomainEventLogger
{
    void Log(string eventName, DomainEventContext context);
}
