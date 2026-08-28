using CouponService.Engine.Ast;

namespace CouponService.Engine.Facts;

public sealed record FactDescriptor(
    string Path,
    ValueKind Kind,
    FactCost Cost,
    Func<EvalScope, CancellationToken, ValueTask<Value>> Resolve);
