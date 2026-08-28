namespace CouponService.EngineTests.Effects;

public sealed class ArchitectureCappedBestOfExampleTests
{
    [Fact]
    public void Fifteen_percent_over_vegetarian_base_beats_flat_five_within_ten_cap()
    {
        var cart = EffectsTestHelper.CreateVegetarianCart(29.00m);
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

        Assert.Equal(5.00m, plan.Total);
        Assert.Equal(5.00m, plan.Allocations.Sum(allocation => allocation.Amount));
    }
}
