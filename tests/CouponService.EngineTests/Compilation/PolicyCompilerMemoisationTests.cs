namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerMemoisationTests
{
    [Fact]
    public async Task Evaluate_resolves_repeated_fact_once_per_evaluation()
    {
        const string conditionJson =
            """
            {
              "all": [
                { "eq": [ { "fact": "customer.confirmedOrderCount" }, { "fact": "customer.confirmedOrderCount" } ] },
                { "eq": [ { "fact": "customer.confirmedOrderCount" }, 0 ] }
              ]
            }
            """;

        var registry = CompilationTestHelper.CreateCountingRegistry("customer.confirmedOrderCount");
        var compiled = CompilationTestHelper.Compile(conditionJson, registry);
        var scope = CompilationTestHelper.CreateScope(confirmedOrderCount: 0, registry: registry);

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.True(result.GetBool());
        Assert.Equal(1, registry.ResolveCount);
    }
}
