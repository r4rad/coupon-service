namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerCostOrderingTests
{
    [Fact]
    public async Task Compile_orders_all_operands_by_fact_cost_before_evaluation()
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
        var scope = CompilationTestHelper.CreateScope(couponUsesTotal: 10, registry: registry);

        await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.Equal(0, registry.ResolveCount);
    }
}
