using CouponService.Engine.Manifest;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

public sealed class PolicyValidatorEngineSchemaTests
{
    [Fact]
    public void Validate_rejects_unsupported_engine_schema()
    {
        var result = ValidationTestHelper.ValidateCondition(
            "2.0",
            """
            { "eq": [ { "fact": "cart.subtotal" }, 25.00 ] }
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("$.engineSchema", error.Path);
        Assert.Contains("2.0", error.Message, StringComparison.Ordinal);
    }
}
