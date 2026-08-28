using CouponService.Engine.Evaluation;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed record EffectScope
{
    public required EvalScope Eval { get; init; }

    public required EffectApplier Applier { get; init; }

    public required SelectorEvaluator Selectors { get; init; }

    public CancellationToken CancellationToken { get; init; } = default;

    public ParseBudget Budget { get; init; } =
        new(EngineLimits.Default.MaxParseNodes, EngineLimits.Default.MaxParseDepth);

    public string EffectPath { get; init; } = "$.effect";
}
