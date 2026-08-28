namespace CouponService.Engine.Manifest;

public sealed record EngineLimits(int MaxParseNodes, int MaxParseDepth)
{
    public static EngineLimits Default { get; } = new(256, 32);
}
