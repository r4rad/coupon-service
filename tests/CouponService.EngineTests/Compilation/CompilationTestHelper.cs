using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Compilation;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;
using CouponService.Engine.Validation;

namespace CouponService.EngineTests.Compilation;

internal static class CompilationTestHelper
{
    internal static PolicyCompiler Compiler { get; } = new();

    internal static IFactRegistry StandardRegistry => StandardFactVocabulary.Create();

    internal static Expr ParseCondition(string json, ParseBudget? budget = null)
    {
        using var document = JsonDocument.Parse(json);
        return PolicyParser.Parse(
            document.RootElement,
            budget ?? new ParseBudget(EngineLimits.Default.MaxParseNodes, EngineLimits.Default.MaxParseDepth),
            PolicyValidator.ConditionPath);
    }

    internal static CompiledCondition Compile(string conditionJson, IFactRegistry? registry = null) =>
        Compiler.Compile(ParseCondition(conditionJson), registry ?? StandardRegistry);

    internal static async Task<Value> EvaluateAsync(
        CompiledCondition compiled,
        EvalScope scope,
        CancellationToken cancellationToken = default) =>
        await compiled.Condition(scope, cancellationToken).ConfigureAwait(false);

    internal static EvalScope CreateScope(
        Cart? cart = null,
        IFactRegistry? registry = null,
        bool captureFullTrace = false,
        DateTimeOffset? utcNow = null,
        int confirmedOrderCount = 0,
        int couponUsesTotal = 0) =>
        EvalScope.Create(
            new FixedClock(utcNow ?? new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero)),
            cart ?? CreateCart(subtotalSeed: 21.90m),
            registry ?? StandardRegistry,
            captureFullTrace: captureFullTrace,
            confirmedOrderCount: confirmedOrderCount,
            isFirstOrder: confirmedOrderCount == 0,
            couponUsesTotal: couponUsesTotal);

    internal static Cart CreateCart(decimal subtotalSeed) =>
        new(ImmutableArray.Create(new CartLine("line-1", "margherita", "classic", subtotalSeed, 1)));

    internal static CountingFactRegistry CreateCountingRegistry(string trackedPath) =>
        new(StandardRegistry, trackedPath);
}

internal sealed class CountingFactRegistry : IFactRegistry
{
    private readonly IFactRegistry _inner;
    private readonly string _trackedPath;
    private int _resolveCount;

    internal CountingFactRegistry(IFactRegistry inner, string trackedPath)
    {
        _inner = inner;
        _trackedPath = trackedPath;
    }

    public int ResolveCount => _resolveCount;

    public IReadOnlyList<FactDescriptor> All => _inner.All;

    public bool TryGet(string path, out FactDescriptor descriptor) => _inner.TryGet(path, out descriptor!);

    public async ValueTask<Value> ResolveAsync(string path, EvalScope scope, CancellationToken cancellationToken)
    {
        if (string.Equals(path, _trackedPath, StringComparison.Ordinal))
        {
            _resolveCount++;
        }

        return await _inner.ResolveAsync(path, scope, cancellationToken).ConfigureAwait(false);
    }
}
