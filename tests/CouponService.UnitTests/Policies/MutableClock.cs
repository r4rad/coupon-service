using CouponService.Domain;

namespace CouponService.UnitTests.Policies;

internal sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    internal void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
