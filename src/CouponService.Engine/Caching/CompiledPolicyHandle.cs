using CouponService.Engine.Compilation;

namespace CouponService.Engine.Caching;

public sealed record CompiledPolicyHandle(string ContentHash, CompiledCondition Condition);
