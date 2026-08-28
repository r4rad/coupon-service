using CouponService.Engine.Ast;

namespace CouponService.EngineTests.Compilation;

public sealed class PolicyCompilerOperatorTests
{
    [Theory]
    [InlineData("""{ "eq": [5, 5] }""", true)]
    [InlineData("""{ "eq": [5, 6] }""", false)]
    [InlineData("""{ "neq": [5, 6] }""", true)]
    [InlineData("""{ "gt": [6, 5] }""", true)]
    [InlineData("""{ "gte": [5, 5] }""", true)]
    [InlineData("""{ "lt": [4, 5] }""", true)]
    [InlineData("""{ "lte": [5, 5] }""", true)]
    public async Task Evaluate_applies_comparison_truth_table(string conditionJson, bool expected)
    {
        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scope = CompilationTestHelper.CreateScope(
            cart: CompilationTestHelper.CreateCart(subtotalSeed: 10m));

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.Equal(expected, result.GetBool());
    }

    [Theory]
    [InlineData("""{ "in": ["Saturday", ["Saturday", "Sunday"]] }""", true)]
    [InlineData("""{ "in": ["Monday", ["Saturday", "Sunday"]] }""", false)]
    [InlineData("""{ "between": [5, 1, 10] }""", true)]
    [InlineData("""{ "between": [11, 1, 10] }""", false)]
    public async Task Evaluate_applies_membership_truth_table(string conditionJson, bool expected)
    {
        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scope = CompilationTestHelper.CreateScope(
            cart: CompilationTestHelper.CreateCart(subtotalSeed: 10m));

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.Equal(expected, result.GetBool());
    }

    [Theory]
    [InlineData("""{ "all": [true, true] }""", true)]
    [InlineData("""{ "all": [true, false] }""", false)]
    [InlineData("""{ "any": [false, true] }""", true)]
    [InlineData("""{ "any": [false, false] }""", false)]
    [InlineData("""{ "not": false }""", true)]
    public async Task Evaluate_applies_logical_truth_table(string conditionJson, bool expected)
    {
        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scope = CompilationTestHelper.CreateScope(
            cart: CompilationTestHelper.CreateCart(subtotalSeed: 10m));

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.Equal(expected, result.GetBool());
    }

    [Theory]
    [InlineData("""{ "add": [1, 2, 3] }""", 6)]
    [InlineData("""{ "sub": [10, 3, 2] }""", 5)]
    [InlineData("""{ "mul": [2, 3, 4] }""", 24)]
    [InlineData("""{ "minOf": [3, 1, 2] }""", 1)]
    [InlineData("""{ "maxOf": [3, 1, 2] }""", 3)]
    public async Task Evaluate_applies_arithmetic_truth_table(string conditionJson, decimal expected)
    {
        var compiled = CompilationTestHelper.Compile(conditionJson);
        var scope = CompilationTestHelper.CreateScope(
            cart: CompilationTestHelper.CreateCart(subtotalSeed: 10m));

        var result = await CompilationTestHelper.EvaluateAsync(compiled, scope);

        Assert.Equal(expected, result.GetNumber());
    }
}
