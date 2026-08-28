namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerShortCircuitTests
{
    [Fact]
    public async Task Evaluate_skips_remote_fact_when_cheap_predicate_fails_first()
    {
        const string conditionJson =
            """
            {
              "all": [
                { "lt": [ { "fact": "coupon.uses.total" }, 1000 ] },
                { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] }
              ]
            }
            """;

        var registry = CompilationTestHelper.CreateCountingRegistry("coupon.uses.total");
        var compiled = CompilationTestHelper.Compile(conditionJson, registry);
        var scope = CompilationTestHelper.CreateScope(couponUsesTotal: 500, registry: registry);

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.False(result.GetBool());
        Assert.Equal(0, registry.ResolveCount);
    }
}
