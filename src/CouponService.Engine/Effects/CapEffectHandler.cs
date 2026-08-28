using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class CapEffectHandler : IEffectHandler
{
    public string Operator => "cap";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Cap effect must be an object.");
        }

        if (!node.TryGetProperty("max", out var maxElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.max", "Cap effect requires 'max'.");
        }

        if (!node.TryGetProperty("of", out var nestedEffect))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.of", "Cap effect requires 'of'.");
        }

        var maximum = maxElement.GetDecimal();
        var nestedPath = $"{scope.EffectPath}.of";
        var nestedPlan = scope.Applier.Apply(nestedEffect, scope with { EffectPath = nestedPath });
        return DiscountPlanBuilder.RescaleToMaximum(nestedPlan, maximum);
    }
}
