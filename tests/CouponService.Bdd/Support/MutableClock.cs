using CouponService.Domain;

namespace CouponService.Bdd.Support;

/// <summary>
/// Mutable clock so automatic day-of-week scenarios and AutomaticPolicyIndex TTL stay deterministic.
/// </summary>
internal sealed class MutableClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Set(DateTimeOffset utcNow) => UtcNow = utcNow;

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}
