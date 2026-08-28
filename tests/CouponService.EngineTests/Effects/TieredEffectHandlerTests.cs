using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.EngineTests.Effects;

public sealed class TieredEffectHandlerTests
{
    [Fact]
    public void Tiered_applies_highest_matching_threshold()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(50.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "tiered": {
                "on": { "fact": "cart.subtotal" },
                "tiers": [
                  { "from": 20.00, "percentage": 10 },
                  { "from": 40.00, "percentage": 15 }
                ]
              }
            }
            """,
            cart);

        Assert.Equal(7.50m, plan.Total);
        Assert.Equal(plan.Allocations.Sum(allocation => allocation.Amount), plan.Total);
    }

    [Fact]
    public void Tiered_applies_discount_when_driving_value_equals_threshold_exactly()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(20.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "tiered": {
                "on": { "fact": "cart.subtotal" },
                "tiers": [
                  { "from": 20.00, "percentage": 10 },
                  { "from": 40.00, "percentage": 15 }
                ]
              }
            }
            """,
            cart);

        Assert.Equal(2.00m, plan.Total);
    }

    [Fact]
    public void Tiered_returns_no_discount_when_no_threshold_is_met()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(19.99m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "tiered": {
                "on": { "fact": "cart.subtotal" },
                "tiers": [
                  { "from": 20.00, "percentage": 10 },
                  { "from": 40.00, "percentage": 15 }
                ]
              }
            }
            """,
            cart);

        Assert.Equal(0m, plan.Total);
        Assert.Empty(plan.Allocations);
    }
}
