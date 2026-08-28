using CouponService.Engine.Manifest;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

public sealed class PolicyValidatorTypeCompatibilityTests
{
    [Fact]
    public void Validate_rejects_comparison_of_incompatible_types_at_write_time()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            { "eq": [ { "fact": "cart.subtotal" }, "Vegetarian" ] }
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("$.condition.eq", error.Path);
        Assert.Contains("incompatible types", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_arithmetic_on_non_numeric_operands()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            { "add": [ { "fact": "time.localDayOfWeek" }, 1 ] }
            """);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Path == "$.condition.add[0]"
                && error.Message.Contains("numeric", StringComparison.OrdinalIgnoreCase));
    }
}
