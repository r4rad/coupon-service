using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class BestOfEffectHandler : IEffectHandler
{
    public string Operator => "bestOf";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Array)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Best-of effect must be an array.");
        }

        var branches = new List<DiscountPlan>();
        var index = 0;
        foreach (var branch in node.EnumerateArray())
        {
            var branchPath = $"{scope.EffectPath}[{index}]";
            branches.Add(scope.Applier.Apply(branch, scope with { EffectPath = branchPath }));
            index++;
        }

        return DiscountPlanBuilder.SelectBest(branches);
    }
}
