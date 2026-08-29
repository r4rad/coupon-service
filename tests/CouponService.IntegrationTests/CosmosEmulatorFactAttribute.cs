namespace CouponService.IntegrationTests;

/// <summary>
/// Marks a fact that requires the local Cosmos emulator. When the emulator is unreachable,
/// the test is skipped with an explicit message so the suite stays green without Docker (AC-10.6, P-9).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CosmosEmulatorFactAttribute : FactAttribute
{
    public CosmosEmulatorFactAttribute()
    {
        if (!CosmosEmulatorGate.IsReachable)
        {
            Skip = CosmosEmulatorGate.SkipReason;
        }
    }
}
