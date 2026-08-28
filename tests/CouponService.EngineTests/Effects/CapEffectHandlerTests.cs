namespace CouponService.EngineTests.Effects;

public sealed class CapEffectHandlerTests
{
    [Fact]
    public void Cap_rescales_allocations_so_they_sum_to_the_capped_total()
    {
        var cart = new CouponService.Domain.Cart(
            System.Collections.Immutable.ImmutableArray.Create(
                new CouponService.Domain.CartLine("line-1", "a", "classic", 1.00m, 1),
                new CouponService.Domain.CartLine("line-2", "b", "classic", 1.00m, 1),
                new CouponService.Domain.CartLine("line-3", "c", "classic", 1.00m, 1)));

        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "cap": {
                "max": 1.00,
                "of": { "fixedAmount": { "amount": 1.50 } }
              }
            }
            """,
            cart);

        Assert.Equal(1.00m, plan.Total);
        Assert.Equal(3, plan.Allocations.Length);
        Assert.Equal(1.00m, plan.Allocations.Sum(allocation => allocation.Amount));
        Assert.Contains(plan.Allocations, allocation => allocation.Amount == 0.34m);
        Assert.Equal(2, plan.Allocations.Count(allocation => allocation.Amount == 0.33m));
    }

    [Fact]
    public void Cap_below_nested_total_reduces_discount_to_ceiling()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(200.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
              "cap": {
                "max": 10.00,
                "of": {
                  "bestOf": [
                    {
                      "percentage": {
                        "value": 15,
                        "of": {
                          "lines": {
                            "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                          }
                        }
                      }
                    },
                    { "fixedAmount": { "amount": 5.00 } }
                  ]
                }
              }
            }
            """,
            cart);

        Assert.Equal(10.00m, plan.Total);
        Assert.Equal(10.00m, plan.Allocations.Sum(allocation => allocation.Amount));
    }
}
