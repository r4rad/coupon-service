using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class FixedAmountEffectHandler : IEffectHandler
{
    public string Operator => "fixedAmount";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Object)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Fixed-amount effect must be an object.");
        }

        if (!node.TryGetProperty("amount", out var amountElement))
        {
            throw new PolicySyntaxException($"{scope.EffectPath}.amount", "Fixed-amount effect requires 'amount'.");
        }

        var amount = amountElement.GetDecimal();
        var weights = scope.Eval.Cart.Lines
            .Select(line => (line.LineId, Money.LineTotal(line.UnitPrice, line.Quantity)))
            .ToArray();

        if (weights.Length == 0)
        {
            return DiscountPlanBuilder.Empty;
        }

        var eligibleBase = weights.Sum(weight => weight.Item2);
        var cappedDiscount = Money.CapDiscount(amount, eligibleBase);
        return DiscountPlanBuilder.AllocateProportionally(weights, cappedDiscount);
    }
}
