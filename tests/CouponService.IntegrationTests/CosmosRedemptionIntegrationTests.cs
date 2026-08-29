using CouponService.Application.Redemption;
using CouponService.Infrastructure.Cosmos;
using Microsoft.Azure.Cosmos;

namespace CouponService.IntegrationTests;

/// <summary>
/// Exercises the real Cosmos data path against the emulator (AC-10.6). Covers behaviours only
/// the database can prove: transactional batch atomicity, unique key, ETag 412, TTL, concurrent reserve (AC-4.5).
/// </summary>
[Collection(CosmosEmulatorCollection.Name)]
public sealed class CosmosRedemptionIntegrationTests(CosmosEmulatorFixture fixture)
{
    [CosmosEmulatorFact]
    public async Task Transactional_batch_rolls_back_redemption_when_counter_etag_mismatches()
    {
        var pk = UniquePk("batch-rollback");
        await SeedCounterAsync(pk, confirmed: 0, active: 0);

        var counter = await fixture.Redemptions.GetCounterAsync(pk);
        Assert.NotNull(counter);

        var orderId = $"order-{Guid.NewGuid():N}";
        var redemption = NewReserved(pk, orderId, "customer-1");

        // Deliberately stale ETag so the replace fails inside the same transactional batch.
        await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            fixture.Redemptions.TryReserveAsync(
                redemption,
                counter! with { ActiveReservations = 1 },
                "\"not-the-real-etag\""));

        var orphan = await fixture.Redemptions.GetByOrderIdAsync(pk, orderId);
        Assert.Null(orphan);

        var after = await fixture.Redemptions.GetCounterAsync(pk);
        Assert.Equal(0, after!.ActiveReservations);
        Assert.Equal(counter.ETag, after.ETag);
    }

    [CosmosEmulatorFact]
    public async Task Duplicate_orderId_violates_unique_key_without_incrementing_counter()
    {
        var pk = UniquePk("unique-order");
        await SeedCounterAsync(pk, confirmed: 0, active: 0);
        var counter = (await fixture.Redemptions.GetCounterAsync(pk))!;

        var orderId = $"order-{Guid.NewGuid():N}";
        await fixture.Redemptions.TryReserveAsync(
            NewReserved(pk, orderId, "customer-1"),
            counter with { ActiveReservations = 1 },
            counter.ETag);

        var afterFirst = (await fixture.Redemptions.GetCounterAsync(pk))!;

        // Same orderId, different document id — unique key on /orderId must reject (not only id conflict).
        var conflicting = new RedemptionDocument
        {
            Id = $"other-{Guid.NewGuid():N}",
            Pk = pk,
            Type = RedemptionDocument.DocumentType,
            OrderId = orderId,
            CustomerId = "customer-2",
            State = nameof(RedemptionState.Reserved),
            DiscountApplied = 1.00m,
            PolicyContentHash = "hash",
            Ttl = 900,
        };

        var conflict = await Assert.ThrowsAsync<CosmosException>(() =>
            fixture.RedemptionsContainer.CreateItemAsync(conflicting, new PartitionKey(pk)));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, conflict.StatusCode);

        var after = await fixture.Redemptions.GetCounterAsync(pk);
        Assert.Equal(1, after!.ActiveReservations);
        Assert.Equal(afterFirst.ETag, after.ETag);
    }

    [CosmosEmulatorFact]
    public async Task Stale_counter_etag_on_reserve_returns_precondition_failed_412()
    {
        var pk = UniquePk("etag-412");
        await SeedCounterAsync(pk, confirmed: 0, active: 0);
        var counter = (await fixture.Redemptions.GetCounterAsync(pk))!;

        await fixture.Redemptions.TryReserveAsync(
            NewReserved(pk, $"order-{Guid.NewGuid():N}", "customer-1"),
            counter with { ActiveReservations = 1 },
            counter.ETag);

        await Assert.ThrowsAsync<PreconditionFailedException>(() =>
            fixture.Redemptions.TryReserveAsync(
                NewReserved(pk, $"order-{Guid.NewGuid():N}", "customer-2"),
                counter with { ActiveReservations = 1 },
                counter.ETag));
    }

    [CosmosEmulatorFact]
    public async Task Reserved_redemption_sets_ttl_and_confirm_clears_it()
    {
        var pk = UniquePk("ttl");
        await SeedCounterAsync(pk, confirmed: 0, active: 0);
        var counter = (await fixture.Redemptions.GetCounterAsync(pk))!;

        var orderId = $"order-{Guid.NewGuid():N}";
        var expires = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        var (redemption, reservedCounter) = await fixture.Redemptions.TryReserveAsync(
            NewReserved(pk, orderId, "customer-1") with { TtlExpiresAt = expires },
            counter with { ActiveReservations = 1 },
            counter.ETag);

        var reservedDoc = await fixture.RedemptionsStore.ReadAsync<RedemptionDocument>(orderId, pk);
        Assert.NotNull(reservedDoc);
        Assert.Equal(900, reservedDoc!.Resource.Ttl);
        Assert.Equal(expires, reservedDoc.Resource.TtlExpiresAt);

        await fixture.Redemptions.TryConfirmAsync(
            redemption with { State = RedemptionState.Confirmed, TtlExpiresAt = null },
            reservedCounter with { ConfirmedCount = 1, ActiveReservations = 0 },
            redemption.ETag,
            reservedCounter.ETag);

        var confirmedDoc = await fixture.RedemptionsStore.ReadAsync<RedemptionDocument>(orderId, pk);
        Assert.NotNull(confirmedDoc);
        Assert.Null(confirmedDoc!.Resource.Ttl);
        Assert.Null(confirmedDoc.Resource.TtlExpiresAt);
        Assert.Equal(nameof(RedemptionState.Confirmed), confirmedDoc.Resource.State);
    }

    [CosmosEmulatorFact]
    public async Task Concurrent_reserves_at_cap_of_one_grant_exactly_one()
    {
        var pk = UniquePk("cap-one");
        await SeedCounterAsync(pk, confirmed: 0, active: 0);
        var counter = (await fixture.Redemptions.GetCounterAsync(pk))!;

        var next = counter with { ActiveReservations = 1 };
        var tasks = Enumerable.Range(0, 8)
            .Select(i => Wrap(fixture.Redemptions.TryReserveAsync(
                NewReserved(pk, $"order-{i}-{Guid.NewGuid():N}", $"customer-{i}"),
                next,
                counter.ETag)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.Equal(7, results.Count(r => r.PreconditionFailed));

        var after = await fixture.Redemptions.GetCounterAsync(pk);
        Assert.Equal(1, after!.ActiveReservations);
    }

    private async Task SeedCounterAsync(string pk, int confirmed, int active)
    {
        await fixture.Redemptions.UpsertCounterAsync(
            new UsageCounterRecord(pk, confirmed, active, ETag: string.Empty),
            "\"seed\"");
    }

    private static string UniquePk(string label) =>
        $"IT-{label}-{Guid.NewGuid():N}";

    private static RedemptionRecord NewReserved(string pk, string orderId, string customerId) =>
        new(
            pk,
            orderId,
            customerId,
            RedemptionState.Reserved,
            DiscountApplied: 3.10m,
            PolicyContentHash: "hash",
            ETag: string.Empty,
            TtlExpiresAt: new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

    private static async Task<(bool Succeeded, bool PreconditionFailed)> Wrap(
        Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return (true, false);
        }
        catch (PreconditionFailedException)
        {
            return (false, true);
        }
    }
}
