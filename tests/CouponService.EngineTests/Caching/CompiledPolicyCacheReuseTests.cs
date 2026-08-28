using CouponService.Domain;
using CouponService.Engine.Caching;

namespace CouponService.EngineTests.Caching;

public sealed class CompiledPolicyCacheReuseTests
{
    [Fact]
    public void GetOrAdd_reuses_compiled_policy_for_unchanged_document()
    {
        var factory = CachingTestHelper.CreateCountingFactory();
        var cache = new CompiledPolicyCache(new FixedClock(DateTimeOffset.UtcNow));

        var first = cache.GetOrAdd(CachingTestHelper.BasePolicyDocument, factory.For(CachingTestHelper.BasePolicyDocument));
        var second = cache.GetOrAdd(CachingTestHelper.ReorderedPolicyDocument, factory.For(CachingTestHelper.ReorderedPolicyDocument));

        Assert.Equal(1, factory.Invocations);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Same(first.Condition, second.Condition);
    }

    [Fact]
    public void GetOrAdd_recompiles_when_document_content_changes()
    {
        var factory = CachingTestHelper.CreateCountingFactory();
        var cache = new CompiledPolicyCache(new FixedClock(DateTimeOffset.UtcNow));

        cache.GetOrAdd(CachingTestHelper.BasePolicyDocument, factory.For(CachingTestHelper.BasePolicyDocument));
        var changed = cache.GetOrAdd(
            CachingTestHelper.ChangedThresholdPolicyDocument,
            factory.For(CachingTestHelper.ChangedThresholdPolicyDocument));

        Assert.Equal(2, factory.Invocations);
        Assert.NotEqual(
            PolicyContentHasher.ComputeHash(CachingTestHelper.BasePolicyDocument),
            changed.ContentHash);
    }

    [Fact]
    public void GetOrAdd_returns_content_hash_for_replay_on_decisions()
    {
        var cache = new CompiledPolicyCache(new FixedClock(DateTimeOffset.UtcNow));
        var factory = CachingTestHelper.CreateCountingFactory();

        var handle = cache.GetOrAdd(CachingTestHelper.BasePolicyDocument, factory.For(CachingTestHelper.BasePolicyDocument));

        Assert.Equal(PolicyContentHasher.ComputeHash(CachingTestHelper.BasePolicyDocument), handle.ContentHash);
        Assert.NotEmpty(handle.ContentHash);
    }
}
