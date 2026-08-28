using CouponService.Engine.Ast;
using CouponService.Engine.Evaluation;

namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerNearMissTests
{
    [Fact]
    public async Task Evaluate_records_numeric_shortfall_on_failed_threshold()
    {
        const string conditionJson = """{ "gte": [ { "fact": "cart.subtotal" }, 25.00 ] }""";

        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scope = CompilationTestHelper.CreateScope();

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.False(result.GetBool());
        var nearMiss = Assert.Single(scope.Trace.NearMisses);
        Assert.Equal("$.condition.gte", nearMiss.Path);
        Assert.Equal(CompareOp.Gte, nearMiss.Operator);
        Assert.Equal(21.90m, nearMiss.Actual);
        Assert.Equal(25.00m, nearMiss.Required);
        Assert.Equal(3.10m, nearMiss.Shortfall);
    }
}
