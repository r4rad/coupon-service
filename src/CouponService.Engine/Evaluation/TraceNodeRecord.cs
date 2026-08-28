using CouponService.Engine.Ast;

namespace CouponService.Engine.Evaluation;

public sealed record TraceNodeRecord(
    string Path,
    string Operator,
    Value Result,
    bool? Passed);
