using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Effects;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Effects;

internal static class EffectsTestHelper
{
    internal static IFactRegistry StandardRegistry => StandardFactVocabulary.Create();

    internal static EvalScope CreateEvalScope(
        Cart? cart = null,
        IFactRegistry? registry = null) =>
        EvalScope.Create(
            new FixedClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero)),
            cart ?? CreateVegetarianCart(29.00m),
            registry ?? StandardRegistry);

    internal static EffectScope CreateEffectScope(
        Cart? cart = null,
        IFactRegistry? registry = null) =>
        EffectEngine.CreateScope(CreateEvalScope(cart, registry));

    internal static DiscountPlan ApplyEffect(string effectJson, Cart? cart = null)
    {
        using var document = JsonDocument.Parse(effectJson);
        var scope = CreateEffectScope(cart);
        return scope.Applier.Apply(document.RootElement, scope);
    }

    internal static DiscountPlan ApplyEffect(JsonElement effect, Cart? cart = null)
    {
        var scope = CreateEffectScope(cart);
        return scope.Applier.Apply(effect, scope);
    }

    internal static Cart CreateVegetarianCart(decimal subtotal)
    {
        var half = Money.Round(subtotal / 2m);
        var remainder = Money.Round(subtotal - half);

        return new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "Vegetarian", half, 1),
            new CartLine("line-2", "funghi", "Vegetarian", remainder, 1)));
    }

    internal static Cart CreateSingleLineCart(decimal lineTotal) =>
        new(ImmutableArray.Create(new CartLine("line-1", "margherita", "Vegetarian", lineTotal, 1)));
}
