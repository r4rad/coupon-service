using CouponService.Domain;

namespace CouponService.UnitTests.Domain;

public sealed class ClockTests
{
    [Fact]
    public void FixedClock_returns_the_same_instant_on_every_read()
    {
        var instant = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedClock(instant);

        Assert.Equal(instant, clock.UtcNow);
        Assert.Equal(instant, clock.UtcNow);
    }
}
