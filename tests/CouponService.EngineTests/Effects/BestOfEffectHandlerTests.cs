namespace CouponService.EngineTests.Effects;

public sealed class BestOfEffectHandlerTests
{
    [Fact]
    public void BestOf_computes_every_branch_and_selects_the_largest_discount()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(29.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
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
            """,
            cart);

        Assert.Equal(5.00m, plan.Total);
        Assert.Equal(plan.Allocations.Sum(allocation => allocation.Amount), plan.Total);
    }

    [Fact]
    public void BestOf_selects_percentage_when_it_beats_flat_amount()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(200.00m);
        var plan = EffectsTestHelper.ApplyEffect(
            """
            {
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
            """,
            cart);

        Assert.Equal(30.00m, plan.Total);
    }
}
