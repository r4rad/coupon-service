using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Compilation;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class TieredEffectHandler : IEffectHandler
{
    private readonly PolicyCompiler _compiler = new();

    public string Operator => "tiered";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Tiered effect must be an object.");
        }

        if (!node.TryGetProperty("on", out var onElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.on", "Tiered effect requires 'on'.");
        }

        if (!node.TryGetProperty("tiers", out var tiersElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.tiers", "Tiered effect requires 'tiers'.");
        }

        if (tiersElement.ValueKind is not JsonValueKind.Array)
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.tiers", "Tiered effect tiers must be an array.");
        }

        var tiers = ParseTiers(tiersElement, scope.EffectPath);
        if (tiers.Count == 0)
        {
            return DiscountPlanBuilder.Empty;
        }

        var onExpression = PolicyParser.Parse(
            onElement,
            scope.Budget,
            $"{scope.EffectPath}.on");

        var compiled = _compiler.Compile(onExpression, scope.Eval.Registry);
        var drivingValue = compiled.Condition(scope.Eval, scope.CancellationToken)
            .GetAwaiter()
            .GetResult()
            .GetNumber();

        var matchingTier = tiers
            .Where(tier => drivingValue >= tier.From)
            .MaxBy(tier => tier.From);

        if (matchingTier is null)
        {
            return DiscountPlanBuilder.Empty;
        }

        var weights = scope.Eval.Cart.Lines
            .Select(line => (line.LineId, Money.LineTotal(line.UnitPrice, line.Quantity)))
            .ToArray();

        if (weights.Length == 0)
        {
            return DiscountPlanBuilder.Empty;
        }

        var eligibleBase = weights.Sum(weight => weight.Item2);
        var discountTotal = Money.Percentage(eligibleBase, matchingTier.Percentage);
        return DiscountPlanBuilder.AllocateProportionally(weights, discountTotal);
    }

    private static List<Tier> ParseTiers(JsonElement tiersElement, string effectPath)
    {
        var tiers = new List<Tier>();
        var index = 0;

        foreach (var tierElement in tiersElement.EnumerateArray())
        {
            var tierPath = $"{effectPath}.tiers[{index}]";
            if (tierElement.ValueKind is not JsonValueKind.Object)
            {
                throw new PolicySyntaxException(tierPath, "Tier entry must be an object.");
            }

            if (!tierElement.TryGetProperty("from", out var fromElement))
            {
                throw new PolicySyntaxException($"{tierPath}.from", "Tier entry requires 'from'.");
            }

            if (!tierElement.TryGetProperty("percentage", out var percentageElement))
            {
                throw new PolicySyntaxException($"{tierPath}.percentage", "Tier entry requires 'percentage'.");
            }

            tiers.Add(new Tier(fromElement.GetDecimal(), percentageElement.GetDecimal()));
            index++;
        }

        return tiers;
    }

    private sealed record Tier(decimal From, decimal Percentage);
}
