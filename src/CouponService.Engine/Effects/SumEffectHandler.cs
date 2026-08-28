using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Parsing;

namespace CouponService.Engine.Effects;

public sealed class SumEffectHandler : IEffectHandler
{
    public string Operator => "sum";

    public DiscountPlan Apply(JsonElement node, EffectScope scope)
    {
        if (node.ValueKind is not JsonValueKind.Array)
        {
            throw new PolicySyntaxException(scope.EffectPath, "Sum effect must be an array.");
        }

        var branches = new List<DiscountPlan>();
        var index = 0;
        foreach (var branch in node.EnumerateArray())
        {
            var branchPath = $"{scope.EffectPath}[{index}]";
            branches.Add(scope.Applier.Apply(branch, scope with { EffectPath = branchPath }));
            index++;
        }

        return DiscountPlanBuilder.SumPlans(branches);
    }
}
