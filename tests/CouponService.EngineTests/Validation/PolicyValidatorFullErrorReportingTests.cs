using CouponService.Engine.Manifest;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

public sealed class PolicyValidatorFullErrorReportingTests
{
    [Fact]
    public void Validate_reports_all_three_condition_errors_in_one_pass()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            {
              "all": [
                { "fact": "customer.zodiacSign" },
                { "eq": [ { "fact": "cart.subtotal" }, "Vegetarian" ] },
                { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
              ]
            }
            """);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Path == "$.condition.all[0].fact" && error.Message.Contains("customer.zodiacSign"));
        Assert.Contains(result.Errors, error => error.Path == "$.condition.all[1].eq" && error.Message.Contains("incompatible types"));
        Assert.Contains(result.Errors, error => error.Path == "$.condition.all[2].eq[0].fact" && error.Message.Contains("cart.lines"));
    }

    [Fact]
    public void Validate_accepts_architecture_example_condition()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            {
              "all": [
                { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] },
                {
                  "every": {
                    "over": "cart.lines",
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                },
                {
                  "any": [
                    { "eq": [ { "fact": "customer.confirmedOrderCount" }, 0 ] },
                    {
                      "in": [
                        { "fact": "time.localDayOfWeek" },
                        ["Saturday", "Sunday"]
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        Assert.True(result.IsValid);
    }
}
