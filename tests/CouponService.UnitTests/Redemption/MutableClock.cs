using CouponService.Domain;

namespace CouponService.UnitTests.Redemption;

internal sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    internal void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
