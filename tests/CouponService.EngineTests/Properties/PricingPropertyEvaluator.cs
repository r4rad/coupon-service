using System.Text.Json;
using CouponService.Application.Pricing;
using CouponService.Domain;
using CouponService.Engine.Effects;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Properties;

internal static class PricingPropertyEvaluator
{
    private static readonly FixedClock Clock =
        new(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));

    private static readonly PriceCalculator Calculator = new();

    internal static (DiscountPlan Plan, PriceBreakdown Breakdown) Evaluate(Cart cart, string effectJson)
    {
        using var document = JsonDocument.Parse(effectJson);
        var scope = EffectEngine.CreateScope(
            EvalScope.Create(Clock, cart, StandardFactVocabulary.Create()));
        var plan = scope.Applier.Apply(document.RootElement, scope);
        var breakdown = Calculator.Calculate(
            cart,
            new PolicyDecision(CouponStatus.Applied, plan));

        return (plan, breakdown);
    }
}
