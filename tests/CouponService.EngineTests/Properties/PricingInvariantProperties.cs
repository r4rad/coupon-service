using CouponService.Application.Pricing;
using CouponService.Domain;
using FsCheck;
using FsCheck.Xunit;

namespace CouponService.EngineTests.Properties;

public sealed class PricingInvariantProperties
{
    private static readonly Arbitrary<Cart> CartArbitrary =
        PricingGenerators.CartGen.ToArbitrary();

    private static readonly Arbitrary<EffectDocument> EffectArbitrary =
        PricingGenerators.EffectJsonGen
            .Select(json => new EffectDocument(json))
            .ToArbitrary();

    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property Discount_is_never_negative() =>
        Prop.ForAll(CartArbitrary, EffectArbitrary, (cart, effect) =>
        {
            var (_, breakdown) = PricingPropertyEvaluator.Evaluate(cart, effect.Json);
            return (breakdown.Discount >= 0m)
                .Label(FormatLabel(cart, effect.Json, breakdown));
        });

    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property Discount_never_exceeds_eligible_base() =>
        Prop.ForAll(CartArbitrary, EffectArbitrary, (cart, effect) =>
        {
            var (_, breakdown) = PricingPropertyEvaluator.Evaluate(cart, effect.Json);
            return (breakdown.Discount <= breakdown.Subtotal)
                .Label(FormatLabel(cart, effect.Json, breakdown));
        });

    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property Allocations_sum_exactly_to_discount_total() =>
        Prop.ForAll(CartArbitrary, EffectArbitrary, (cart, effect) =>
        {
            var (plan, _) = PricingPropertyEvaluator.Evaluate(cart, effect.Json);
            var allocationSum = Money.Round(plan.Allocations.Sum(allocation => allocation.Amount));

            return (allocationSum == plan.Total && plan.Allocations.All(allocation => allocation.Amount >= 0m))
                .Label(FormatLabel(cart, effect.Json, plan, allocationSum));
        });

    [Property(MaxTest = 200, QuietOnSuccess = true)]
    public Property Total_plus_discount_equals_subtotal() =>
        Prop.ForAll(CartArbitrary, EffectArbitrary, (cart, effect) =>
        {
            var (_, breakdown) = PricingPropertyEvaluator.Evaluate(cart, effect.Json);
            return (breakdown.Total + breakdown.Discount == breakdown.Subtotal)
                .Label(FormatLabel(cart, effect.Json, breakdown));
        });

    [Property(MaxTest = 100, QuietOnSuccess = true)]
    public Property Evaluation_with_fixed_clock_is_deterministic() =>
        Prop.ForAll(CartArbitrary, EffectArbitrary, (cart, effect) =>
        {
            var first = PricingPropertyEvaluator.Evaluate(cart, effect.Json);
            var second = PricingPropertyEvaluator.Evaluate(cart, effect.Json);

            var plansMatch =
                first.Plan.Total == second.Plan.Total
                && first.Plan.Allocations.Length == second.Plan.Allocations.Length
                && first.Plan.Allocations.All(firstAllocation =>
                    second.Plan.Allocations.Any(secondAllocation =>
                        secondAllocation.LineId == firstAllocation.LineId
                        && secondAllocation.Amount == firstAllocation.Amount));

            return plansMatch.Label(FormatLabel(cart, effect.Json, first.Plan, second.Plan));
        });

    private static string FormatLabel(Cart cart, string effectJson, PriceBreakdown breakdown) =>
        $"""
        subtotal={breakdown.Subtotal}, discount={breakdown.Discount}, total={breakdown.Total}
        cart={FormatCart(cart)}
        effect={effectJson}
        """;

    private static string FormatLabel(Cart cart, string effectJson, DiscountPlan plan, decimal allocationSum) =>
        $"""
        planTotal={plan.Total}, allocationSum={allocationSum}
        cart={FormatCart(cart)}
        effect={effectJson}
        """;

    private static string FormatLabel(
        Cart cart,
        string effectJson,
        DiscountPlan firstPlan,
        DiscountPlan secondPlan) =>
        $"""
        firstTotal={firstPlan.Total}, secondTotal={secondPlan.Total}
        cart={FormatCart(cart)}
        effect={effectJson}
        """;

    private static string FormatCart(Cart cart) =>
        string.Join(
            "; ",
            cart.Lines.Select(line =>
                $"{line.LineId}:{line.Category}x{line.Quantity}@{line.UnitPrice}"));
}
