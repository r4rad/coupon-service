using CouponService.Engine.Caching;

namespace CouponService.EngineTests.Caching;

public sealed class NegativeLookupCacheTests
{
    [Fact]
    public void RememberFailure_blocks_lookup_until_ttl_expires()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var cache = new NegativeLookupCache(clock, new NegativeLookupCacheOptions(MaxEntries: 10, Ttl: TimeSpan.FromSeconds(30)));

        cache.RememberFailure("MISSING");

        Assert.True(cache.IsBlocked("MISSING"));

        clock.Advance(TimeSpan.FromSeconds(31));

        Assert.False(cache.IsBlocked("MISSING"));
    }

    [Fact]
    public void RememberFailure_enforces_a_maximum_entry_count()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var cache = new NegativeLookupCache(clock, new NegativeLookupCacheOptions(MaxEntries: 2, Ttl: TimeSpan.FromMinutes(5)));

        cache.RememberFailure("A");
        cache.RememberFailure("B");
        cache.RememberFailure("C");

        Assert.False(cache.IsBlocked("A"));
        Assert.True(cache.IsBlocked("B"));
        Assert.True(cache.IsBlocked("C"));
    }
}
