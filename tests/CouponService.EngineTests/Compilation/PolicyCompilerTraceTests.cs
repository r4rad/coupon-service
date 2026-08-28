namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerTraceTests
{
    [Fact]
    public async Task Evaluate_builds_node_trace_only_when_requested()
    {
        const string conditionJson = """{ "gte": [ { "fact": "cart.subtotal" }, 25.00 ] }""";

        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scopeWithoutTrace = CompilationTestHelper.CreateScope(captureFullTrace: false);
        var scopeWithTrace = CompilationTestHelper.CreateScope(captureFullTrace: true);

        await CompilationTestHelper.EvaluateAsync(compiled, scopeWithoutTrace);
        await CompilationTestHelper.EvaluateAsync(compiled, scopeWithTrace);

        Assert.Empty(scopeWithoutTrace.Trace.ToEvaluationTrace().Nodes);
        Assert.NotEmpty(scopeWithTrace.Trace.ToEvaluationTrace().Nodes);
        Assert.NotEmpty(scopeWithTrace.Trace.NearMisses);
    }
}
