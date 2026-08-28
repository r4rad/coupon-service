using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;

namespace CouponService.EngineTests.Facts;

public sealed class EngineManifestTests
{
    [Fact]
    public void Generate_lists_every_registered_fact_with_type_and_cost()
    {
        var registry = StandardFactVocabulary.Create();
        var manifest = EngineManifestGenerator.Generate(registry, EngineLimits.Default);

        Assert.Equal(registry.All.Count, manifest.Facts.Count);

        foreach (var descriptor in registry.All)
        {
            Assert.Contains(
                manifest.Facts,
                fact => fact.Path == descriptor.Path
                    && fact.Kind == descriptor.Kind
                    && fact.Cost == descriptor.Cost);
        }
    }

    [Fact]
    public void Generate_lists_every_condition_operator_effect_and_configured_limit()
    {
        var registry = StandardFactVocabulary.Create();
        var limits = new EngineLimits(128, 16);
        var manifest = EngineManifestGenerator.Generate(registry, limits);

        Assert.Equal(EngineManifestGenerator.CurrentEngineSchema, manifest.EngineSchema);
        Assert.Equal(EngineCatalog.ConditionOperators, manifest.ConditionOperators);
        Assert.Equal(EngineCatalog.EffectOperators, manifest.EffectOperators);
        Assert.Equal(limits, manifest.Limits);
    }
}
