using CouponService.Engine.Evaluation;

namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerDeterminismTests
{
    [Fact]
    public async Task Evaluate_produces_identical_results_for_identical_inputs_with_fixed_clock()
    {
        const string conditionJson =
            """
            {
              "all": [
                { "gte": [ { "fact": "cart.subtotal" }, 20.00 ] },
                { "in": [ { "fact": "time.localDayOfWeek" }, ["Friday", "Saturday"] ] }
              ]
            }
            """;

        var compiled = CompilationTestHelper.Compile(conditionJson);
        var instant = new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero);
        var cart = CompilationTestHelper.CreateCart(21.90m);

        var firstScope = CompilationTestHelper.CreateScope(cart, utcNow: instant);
        var secondScope = CompilationTestHelper.CreateScope(cart, utcNow: instant);

        var first = await CompilationTestHelper.EvaluateAsync(compiled, firstScope);
        var second = await CompilationTestHelper.EvaluateAsync(compiled, secondScope);

        Assert.Equal(first, second);
        Assert.Equal(firstScope.Trace.NearMisses.Count, secondScope.Trace.NearMisses.Count);
    }
}
