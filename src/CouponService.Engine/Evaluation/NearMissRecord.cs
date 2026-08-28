using CouponService.Engine.Ast;

namespace CouponService.Engine.Evaluation;

public sealed record NearMissRecord(
    string Path,
    CompareOp Operator,
    decimal Actual,
    decimal Required,
    decimal Shortfall);
