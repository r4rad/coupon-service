using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class NthItemEffectHandler : IEffectHandler
{
    public string Operator => "nthItem";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Nth-item effect must be an object.");
        }

        if (!node.TryGetProperty("from", out var selectorElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.from", "Nth-item effect requires 'from'.");
        }

        if (!node.TryGetProperty("n", out var nElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.n", "Nth-item effect requires 'n'.");
        }

        if (!node.TryGetProperty("percentage", out var percentageElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.percentage", "Nth-item effect requires 'percentage'.");
        }

        var interval = nElement.GetInt32();
        if (interval <= 0)
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.n", "Nth-item interval must be greater than zero.");
        }

        var percentage = percentageElement.GetDecimal();
        var selectedLines = scope.Selectors.SelectLines(
            selectorElement,
            $"{scope.EffectPath}.from",
            scope.Budget,
            scope.Eval,
            scope.CancellationToken);

        if (selectedLines.IsEmpty)
        {
            return DiscountPlanBuilder.Empty;
        }

        var units = EffectUnitExpander.Expand(selectedLines).ToArray();
        var discountsByLine = new Dictionary<string, decimal>(StringComparer.Ordinal);

        for (var index = 0; index < units.Length; index++)
        {
            if ((index + 1) % interval != 0)
            {
                continue;
            }

            var unit = units[index];
            var unitDiscount = Money.Percentage(unit.UnitPrice, percentage);
            discountsByLine[unit.LineId] =
                discountsByLine.GetValueOrDefault(unit.LineId) + unitDiscount;
        }

        if (discountsByLine.Count == 0)
        {
            return DiscountPlanBuilder.Empty;
        }

        var allocations = discountsByLine
            .Select(pair => new LineAllocation(pair.Key, Money.Round(pair.Value)))
            .Where(allocation => allocation.Amount > 0)
            .ToImmutableArray();

        var total = Money.Round(allocations.Sum(allocation => allocation.Amount));
        return new DiscountPlan(total, allocations);
    }
}
