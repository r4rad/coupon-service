using CouponService.Domain;
using CouponService.Engine.Compilation;
using CouponService.Engine.Facts;

namespace CouponService.EngineTests.Caching;

internal sealed class TestClock(DateTimeOffset start) : IClock
{
    private DateTimeOffset _utcNow = start;

    public DateTimeOffset UtcNow => _utcNow;

    internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}

internal static class CachingTestHelper
{
    internal const string BasePolicyDocument =
        """
        {
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] }
        }
        """;

    internal const string ReorderedPolicyDocument =
        """
        {"condition":{"gte":[{"fact":"cart.subtotal"},25.00]},"engineSchema":"1.0"}
        """;

    internal const string ChangedThresholdPolicyDocument =
        """
        {
          "engineSchema": "1.0",
          "condition": { "gte": [ { "fact": "cart.subtotal" }, 30.00 ] }
        }
        """;

    internal static PolicyCompiler Compiler { get; } = new();

    internal static IFactRegistry Registry => StandardFactVocabulary.Create();

    internal static CompiledCondition CompileCondition(string policyDocumentJson) =>
        Compiler.Compile(
            Compilation.CompilationTestHelper.ParseCondition(ExtractConditionJson(policyDocumentJson)),
            Registry);

    internal static string ExtractConditionJson(string policyDocumentJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(policyDocumentJson);
        return document.RootElement.GetProperty("condition").GetRawText();
    }

    internal static CountingCompileFactory CreateCountingFactory() => new();

    internal sealed class CountingCompileFactory
    {
        private int _invocations;

        internal int Invocations => _invocations;

        internal Func<CompiledCondition> For(string policyDocumentJson) =>
            () =>
            {
                _invocations++;
                return CompileCondition(policyDocumentJson);
            };
    }
}
