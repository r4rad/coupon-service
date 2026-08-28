using CouponService.Application.Redemption;
using CouponService.Infrastructure.InMemory;

namespace CouponService.UnitTests.Application;

public sealed class InMemoryRedemptionRepositoryTests
{
    [Fact]
    public async Task Upsert_counter_with_stale_etag_throws_precondition_failed()
    {
        var repository = new InMemoryRedemptionRepository();
        var created = await repository.UpsertCounterAsync(
            new UsageCounterRecord("SAVE10", 0, 0, string.Empty),
            "\"seed\"");

        var updated = await repository.UpsertCounterAsync(
            new UsageCounterRecord("SAVE10", 1, 0, created.ETag),
            created.ETag);

        await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            repository.UpsertCounterAsync(
                new UsageCounterRecord("SAVE10", 2, 0, updated.ETag),
                created.ETag));
    }
}
