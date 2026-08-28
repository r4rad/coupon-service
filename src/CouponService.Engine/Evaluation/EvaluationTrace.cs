using System.Collections.Immutable;

namespace CouponService.Engine.Evaluation;

public sealed record EvaluationTrace(
    ImmutableArray<TraceNodeRecord> Nodes,
    ImmutableArray<NearMissRecord> NearMisses);
