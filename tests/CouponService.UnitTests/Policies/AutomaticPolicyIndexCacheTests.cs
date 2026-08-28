using CouponService.Application.Policies;
using CouponService.Infrastructure.InMemory;

namespace CouponService.UnitTests.Policies;

public sealed class AutomaticPolicyIndexCacheTests
{
    [Fact]
    public async Task Cache_is_not_requeried_within_sixty_seconds()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryPolicyRepository();
        await repository.CreateAsync(PolicyRecordFactory.FromDocument(PoliciesTestDocuments.TuesdayAutomatic));

        var index = new AutomaticPolicyIndex(repository, clock);

        _ = await index.GetAutomaticPoliciesAsync();
        _ = await index.GetAutomaticPoliciesAsync();
        clock.Advance(TimeSpan.FromSeconds(30));
        _ = await index.GetAutomaticPoliciesAsync();

        Assert.Equal(1, repository.AutomaticQueryCount);
    }

    [Fact]
    public async Task Cache_is_refreshed_after_sixty_seconds()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryPolicyRepository();
        await repository.CreateAsync(PolicyRecordFactory.FromDocument(PoliciesTestDocuments.TuesdayAutomatic));

        var index = new AutomaticPolicyIndex(repository, clock);

        _ = await index.GetAutomaticPoliciesAsync();
        clock.Advance(TimeSpan.FromSeconds(61));
        _ = await index.GetAutomaticPoliciesAsync();

        Assert.Equal(2, repository.AutomaticQueryCount);
    }
}
