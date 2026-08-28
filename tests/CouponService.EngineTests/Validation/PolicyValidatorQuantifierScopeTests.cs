using CouponService.Engine.Manifest;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

public sealed class PolicyValidatorQuantifierScopeTests
{
    [Fact]
    public void Validate_rejects_line_facts_outside_quantifier_scope()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            { "fact": "line.category" }
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("$.condition.fact", error.Path);
        Assert.Contains("cart.lines", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_allows_line_facts_inside_quantifier_over_cart_lines()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            {
              "every": {
                "over": "cart.lines",
                "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
              }
            }
            """);

        Assert.True(result.IsValid);
    }
}
