using System.Collections.Immutable;
using CouponService.Application.Engine;
using CouponService.Application.Policies;
using CouponService.Application.Redemption;
using CouponService.Domain;
using CouponService.Engine.Caching;
using CouponService.Engine.Facts;
using CouponService.Infrastructure.InMemory;
using PolicyEngine = CouponService.Application.Engine.PolicyEngine;

namespace CouponService.UnitTests.Policies;

internal sealed class PoliciesTestContext
{
    internal PoliciesTestContext(DateTimeOffset utcNow)
    {
        Clock = new FixedClock(utcNow);
        Policies = new InMemoryPolicyRepository();
        var redemptions = new InMemoryRedemptionRepository();
        var registry = StandardFactVocabulary.Create();
        Engine = new PolicyEngine(Clock, registry, redemptions, new CompiledPolicyCache(Clock));
        Index = new AutomaticPolicyIndex(Policies, Clock);
        Resolver = new PolicyCandidateResolver();
        AutomaticPreview = new AutomaticPolicyPreviewService(Index, Engine, Resolver);
    }

    internal IClock Clock { get; }

    internal InMemoryPolicyRepository Policies { get; }

    internal PolicyEngine Engine { get; }

    internal AutomaticPolicyIndex Index { get; }

    internal PolicyCandidateResolver Resolver { get; }

    internal AutomaticPolicyPreviewService AutomaticPreview { get; }

    internal static Cart CreateStandardCart() =>
        new(ImmutableArray.Create(
            new CartLine("line-1", "margherita", "classic", 9.50m, 2),
            new CartLine("line-2", "bbq-chicken", "meat", 12.00m, 1)));

    internal async Task SeedAsync(string documentJson)
    {
        var record = PolicyRecordFactory.FromDocument(documentJson);
        await Policies.CreateAsync(record);
    }
}
