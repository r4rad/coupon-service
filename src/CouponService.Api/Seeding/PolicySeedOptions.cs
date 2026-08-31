namespace CouponService.Api.Seeding;

public sealed class PolicySeedOptions
{
    public const string SectionName = "Seeding";

    /// <summary>
    /// Seeds the deterministic policy set as the host starts (AC-9.5, AC-9.6). The policy store
    /// is per-instance, so every replica seeds its own copy and a restart re-converges.
    /// </summary>
    public bool Enabled { get; init; }
}
