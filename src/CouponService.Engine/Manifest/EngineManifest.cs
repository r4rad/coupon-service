using CouponService.Engine.Ast;
using CouponService.Engine.Facts;

namespace CouponService.Engine.Manifest;

public sealed record ManifestFact(string Path, ValueKind Kind, FactCost Cost);

public sealed record EngineManifest(
    string EngineSchema,
    IReadOnlyList<ManifestFact> Facts,
    IReadOnlyList<string> ConditionOperators,
    IReadOnlyList<string> EffectOperators,
    EngineLimits Limits);

public static class EngineManifestGenerator
{
    public const string CurrentEngineSchema = "1.0";

    public static EngineManifest Generate(IFactRegistry registry, EngineLimits limits) =>
        new(
            CurrentEngineSchema,
            registry.All
                .Select(fact => new ManifestFact(fact.Path, fact.Kind, fact.Cost))
                .ToArray(),
            EngineCatalog.ConditionOperators.ToArray(),
            EngineCatalog.EffectOperators.ToArray(),
            limits);
}
