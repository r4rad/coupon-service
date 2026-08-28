using System.Collections.Immutable;
using CouponService.Domain;

namespace CouponService.EngineTests.Effects;

public sealed class CheapestFreeEffectHandlerTests
{
    [Fact]
    public void CheapestFree_expands_quantities_when_selecting_free_units()
    {
        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "Vegetarian", 5.00m, 3),
            new CartLine("line-2", "funghi", "Vegetarian", 8.00m, 1)));

        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "cheapestFree": {
                "count": 2,
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
        Assert.Equal(10.00m, plan.Allocations.Single(allocation => allocation.LineId == "line-1").Amount);
    }

    [Fact]
    public void CheapestFree_resolves_equal_unit_price_ties_by_line_id()
    {
        var cart = new Cart(ImmutableArray.Create(
            new CartLine("line-b", "funghi", "Vegetarian", 10.00m, 1),
            new CartLine("line-a", "margherita", "Vegetarian", 10.00m, 1)));

        var plan = EffectsTestHelper.ApplyEffect(
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

        Assert.Equal(10.00m, plan.Total);
        Assert.Equal("line-a", plan.Allocations.Single().LineId);
    }

    [Fact]
    public void CheapestFree_returns_no_discount_when_selector_matches_nothing()
    {
        var cart = EffectsTestHelper.CreateSingleLineCart(12.00m, category: "Meat");
        var plan = EffectsTestHelper.ApplyEffect(
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

        Assert.Equal(0m, plan.Total);
        Assert.Empty(plan.Allocations);
    }
}
