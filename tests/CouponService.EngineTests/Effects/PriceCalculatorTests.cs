using CouponService.Application.Pricing;
using CouponService.Domain;

namespace CouponService.EngineTests.Effects;

public sealed class PriceCalculatorTests
{
    [Fact]
    public void Applied_decision_produces_subtotal_discount_total_and_per_line_allocations()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(31.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "percentage": {
                "value": 10,
                "of": {
                  "lines": {
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                }
              }
            }
            """,
            cart);

        var breakdown = new PriceCalculator().Calculate(
            cart,
            PolicyDecision.Applied(plan, "test-hash"));

        Assert.Equal(31.00m, breakdown.Subtotal);
        Assert.Equal(3.10m, breakdown.Discount);
        Assert.Equal(27.90m, breakdown.Total);
        Assert.Equal(3.10m, plan.Total);
        Assert.Equal(plan.Allocations.Sum(allocation => allocation.Amount), plan.Total);

        var firstAllocation = plan.Allocations.First(allocation => allocation.LineId == "line-1");
        var secondAllocation = plan.Allocations.First(allocation => allocation.LineId == "line-2");
        Assert.Equal(1.55m, firstAllocation.Amount);
        Assert.Equal(1.55m, secondAllocation.Amount);
    }

    [Fact]
    public void Rejected_decision_returns_full_price_breakdown_without_coupon_code()
    {
        var cart = EffectsTestHelper.CreateSingleLineCart(12.00m);
        var breakdown = new PriceCalculator().Calculate(
            cart,
            PolicyDecision.Rejected(RejectionReason.MinimumOrderNotMet, "test-hash"));

        Assert.Equal(12.00m, breakdown.Subtotal);
        Assert.Equal(0m, breakdown.Discount);
        Assert.Equal(12.00m, breakdown.Total);
    }
}
