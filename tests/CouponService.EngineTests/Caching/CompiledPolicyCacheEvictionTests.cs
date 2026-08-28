using CouponService.Domain;
using CouponService.Engine.Caching;

namespace CouponService.EngineTests.Caching;

public sealed class CompiledPolicyCacheEvictionTests
{
    [Fact]
    public void GetOrAdd_evicts_entries_when_size_bound_is_exceeded()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var cache = new CompiledPolicyCache(clock, new CompiledPolicyCacheOptions(MaxEntries: 2, SlidingExpiration: TimeSpan.FromMinutes(30)));

        var factory = CachingTestHelper.CreateCountingFactory();
        var firstHash = cache.GetOrAdd(
            """
            { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 10.00 ] } }
            """,
            factory.For(
                """
                { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 10.00 ] } }
                """)).ContentHash;

        cache.GetOrAdd(
            """
            { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 20.00 ] } }
            """,
            factory.For(
                """
                { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 20.00 ] } }
                """));

        cache.GetOrAdd(
            """
            { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 30.00 ] } }
            """,
            factory.For(
                """
                { "engineSchema": "1.0", "condition": { "gte": [ { "fact": "cart.subtotal" }, 30.00 ] } }
                """));

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet(firstHash, out _));
    }

    [Fact]
    public void TryGet_misses_after_sliding_expiration_elapses()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var cache = new CompiledPolicyCache(clock, new CompiledPolicyCacheOptions(MaxEntries: 4, SlidingExpiration: TimeSpan.FromMinutes(5)));
        var factory = CachingTestHelper.CreateCountingFactory();

        var handle = cache.GetOrAdd(CachingTestHelper.BasePolicyDocument, factory.For(CachingTestHelper.BasePolicyDocument));

        clock.Advance(TimeSpan.FromMinutes(6));

        Assert.False(cache.TryGet(handle.ContentHash, out _));
    }
}
