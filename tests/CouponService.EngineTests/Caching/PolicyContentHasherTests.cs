using CouponService.Engine.Caching;

namespace CouponService.EngineTests.Caching;

public sealed class PolicyContentHasherTests
{
    [Fact]
    public void ComputeHash_is_identical_for_reordered_keys_and_whitespace()
    {
        var first = PolicyContentHasher.ComputeHash(CachingTestHelper.BasePolicyDocument);
        var second = PolicyContentHasher.ComputeHash(CachingTestHelper.ReorderedPolicyDocument);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHash_changes_when_a_threshold_changes()
    {
        var original = PolicyContentHasher.ComputeHash(CachingTestHelper.BasePolicyDocument);
        var changed = PolicyContentHasher.ComputeHash(CachingTestHelper.ChangedThresholdPolicyDocument);

        Assert.NotEqual(original, changed);
    }
}
