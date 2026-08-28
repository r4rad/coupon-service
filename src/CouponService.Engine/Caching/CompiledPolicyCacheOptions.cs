namespace CouponService.Engine.Caching;

public sealed record CompiledPolicyCacheOptions(int MaxEntries, TimeSpan SlidingExpiration)
{
    public static CompiledPolicyCacheOptions Default { get; } = new(128, TimeSpan.FromMinutes(30));
}
