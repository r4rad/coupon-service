using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.EngineTests.Effects;

public sealed class AdvancedEffectGrammarTests
{
    [Fact]
    public void Effect_grammar_accepts_cheapestFree_nthItem_and_tiered_operators()
    {
        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "Vegetarian", 10.00m, 3),
            new CartLine("line-2", "pepperoni", "Meat", 12.00m, 1)));

        var cheapestFreePlan = EffectsTestHelper.ApplyEffect(
            """
            {
              "cheapestFree": {
                "count": 1,
                "from": {
                  "lines": {
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                }
              }
            }
            """,
            cart);
        Assert.Equal(10.00m, cheapestFreePlan.Total);

        var nthItemPlan = EffectsTestHelper.ApplyEffect(
            """
            {
              "nthItem": {
                "n": 3,
                "percentage": 100,
                "from": {
                  "lines": {
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                }
              }
            }
            """,
            cart);
        Assert.Equal(10.00m, nthItemPlan.Total);

        var tieredPlan = EffectsTestHelper.ApplyEffect(
            """
            {
              "tiered": {
                "on": { "fact": "cart.subtotal" },
                "tiers": [
                  { "from": 20.00, "percentage": 10 },
                  { "from": 30.00, "percentage": 15 }
                ]
              }
            }
            """,
            cart);
        Assert.Equal(6.30m, tieredPlan.Total);
    }
}
