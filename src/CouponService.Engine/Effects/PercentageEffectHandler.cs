using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class PercentageEffectHandler : IEffectHandler
{
    public string Operator => "percentage";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Percentage effect must be an object.");
        }

        if (!node.TryGetProperty("value", out var valueElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.value", "Percentage effect requires 'value'.");
        }

        if (!node.TryGetProperty("of", out var selectorElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.of", "Percentage effect requires 'of'.");
        }

        var percentage = valueElement.GetDecimal();
        var selectedLines = scope.Selectors.SelectLines(
            selectorElement,
            $"{scope.EffectPath}.of",
            scope.Budget,
            scope.Eval,
            scope.CancellationToken);

        if (selectedLines.IsEmpty)
        {
            return DiscountPlanBuilder.Empty;
        }

        var weights = selectedLines
            .Select(line => (line.LineId, Money.LineTotal(line.UnitPrice, line.Quantity)))
            .ToArray();

        var baseAmount = weights.Sum(weight => weight.Item2);
        var discountTotal = Money.Percentage(baseAmount, percentage);
        return DiscountPlanBuilder.AllocateProportionally(weights, discountTotal);
    }
}
