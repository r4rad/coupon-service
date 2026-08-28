namespace CouponService.Engine.Caching;

public sealed record NegativeLookupCacheOptions(int MaxEntries, TimeSpan Ttl)
{
    public static NegativeLookupCacheOptions Default { get; } = new(1_000, TimeSpan.FromSeconds(60));
}
