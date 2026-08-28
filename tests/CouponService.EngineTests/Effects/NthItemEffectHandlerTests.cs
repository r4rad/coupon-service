using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.EngineTests.Effects;

public sealed class NthItemEffectHandlerTests
{
    [Fact]
    public void NthItem_applies_buy_two_get_one_by_discounting_every_third_unit()
    {
        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "Vegetarian", 10.00m, 3)));

        var plan = EffectsTestHelper.ApplyEffect(
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

        Assert.Equal(10.00m, plan.Total);
        Assert.Equal(10.00m, plan.Allocations.Single().Amount);
    }

    [Fact]
    public void NthItem_discounts_multiple_qualifying_units_across_expanded_quantities()
    {
        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "Vegetarian", 10.00m, 6)));

        var plan = EffectsTestHelper.ApplyEffect(
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

        Assert.Equal(20.00m, plan.Total);
        Assert.Equal(20.00m, plan.Allocations.Single().Amount);
    }

    [Fact]
    public void NthItem_returns_no_discount_when_selector_matches_nothing()
    {
        var cart = EffectsTestHelper.CreateSingleLineCart(10.00m, category: "Meat");
        var plan = EffectsTestHelper.ApplyEffect(
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

        Assert.Equal(0m, plan.Total);
        Assert.Empty(plan.Allocations);
    }
}
