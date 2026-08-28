namespace CouponService.Engine.Effects;

public static class EffectEngine
{
    public static EffectApplier CreateStandardApplier() =>
        new EffectApplier(CreateStandardHandlers());

    public static IReadOnlyList<IEffectHandler> CreateStandardHandlers() =>
    [
        new PercentageEffectHandler(),
        new FixedAmountEffectHandler(),
        new CheapestFreeEffectHandler(),
        new NthItemEffectHandler(),
        new TieredEffectHandler(),
        new SumEffectHandler(),
        new BestOfEffectHandler(),
        new CapEffectHandler(),
    ];

    public static EffectScope CreateScope(Evaluation.EvalScope evalScope) =>
        new()
        {
            Eval = evalScope,
            Applier = CreateStandardApplier(),
            Selectors = new SelectorEvaluator(),
        };
}
