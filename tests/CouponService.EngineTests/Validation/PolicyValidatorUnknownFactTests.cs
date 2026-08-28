using CouponService.Engine.Manifest;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Validation;

public sealed class PolicyValidatorUnknownFactTests
{
    [Fact]
    public void Validate_reports_unknown_fact_path_and_json_location()
    {
        var result = ValidationTestHelper.ValidateCondition(
            EngineManifestGenerator.CurrentEngineSchema,
            """
            { "fact": "customer.zodiacSign" }
            """);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("$.condition.fact", error.Path);
        Assert.Contains("customer.zodiacSign", error.Message, StringComparison.Ordinal);
    }
}
