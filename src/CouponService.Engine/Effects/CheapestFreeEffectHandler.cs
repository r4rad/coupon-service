using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class CheapestFreeEffectHandler : IEffectHandler
{
    public string Operator => "cheapestFree";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Cheapest-free effect must be an object.");
        }

        if (!node.TryGetProperty("from", out var selectorElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.from", "Cheapest-free effect requires 'from'.");
        }

        if (!node.TryGetProperty("count", out var countElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.count", "Cheapest-free effect requires 'count'.");
        }

        var count = countElement.GetInt32();
        if (count <= 0)
        {
            return DiscountPlanBuilder.Empty;
        }

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

        var freeUnits = EffectUnitExpander.Expand(selectedLines)
            .OrderBy(unit => unit.UnitPrice)
            .ThenBy(unit => unit.LineId, StringComparer.Ordinal)
            .ThenBy(unit => unit.UnitIndex)
            .Take(count)
            .ToArray();

        if (freeUnits.Length == 0)
        {
            return DiscountPlanBuilder.Empty;
        }

        var discountsByLine = freeUnits
            .GroupBy(unit => unit.LineId)
            .Select(group => new LineAllocation(
                group.Key,
                Money.Round(group.Sum(unit => unit.UnitPrice))))
            .Where(allocation => allocation.Amount > 0)
            .ToImmutableArray();

        var total = Money.Round(discountsByLine.Sum(allocation => allocation.Amount));
        return new DiscountPlan(total, discountsByLine);
    }
}
