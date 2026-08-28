using CouponService.Engine.Facts;

namespace CouponService.Engine.Compilation;

public sealed record CompiledCondition(Compiled Condition, FactCost MaxCost);
