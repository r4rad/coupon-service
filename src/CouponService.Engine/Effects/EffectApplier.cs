using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class EffectApplier
{
    private readonly IReadOnlyDictionary<string, IEffectHandler> _handlers;

    public EffectApplier(IEnumerable<IEffectHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.Operator, StringComparer.Ordinal);
    }

    public DiscountPlan Apply(JsonElement effect, EffectScope scope)
    {
        if (effect.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Effect must be a JSON object.");
        }

        using var enumerator = effect.EnumerateObject();
        if (!enumerator.MoveNext())
        {
            throw new PolicySyntaxException(scope.EffectPath, "Effect must contain exactly one operator.");
        }

        var property = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new PolicySyntaxException(scope.EffectPath, "Effect must contain exactly one operator.");
        }

        if (!_handlers.TryGetValue(property.Name, out var handler))
        {
            throw new PolicySyntaxException(scope.EffectPath, $"Unknown effect operator '{property.Name}'.");
        }

        var operatorPath = $"{scope.EffectPath}.{property.Name}";
        return handler.Apply(property.Value, scope with { EffectPath = operatorPath });
    }
}
