using CouponService.Application.Redemption;
using Microsoft.Extensions.Logging;

namespace CouponService.Infrastructure.Cosmos;

public sealed class CosmosRedemptionRepository(ICosmosItemStore store, ILogger<CosmosRedemptionRepository> logger)
    : IRedemptionRepository
{
    public async Task<UsageCounterRecord?> GetCounterAsync(
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var result = await store
            .ReadAsync<CounterDocument>(CounterDocument.DocumentId, partitionKey, cancellationToken)
            .ConfigureAwait(false);

        return result is null ? null : CosmosDocumentMapper.ToRecord(result.Resource, result.ETag);
    }

    public async Task<UsageCounterRecord> UpsertCounterAsync(
        UsageCounterRecord counter,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        var document = CosmosDocumentMapper.ToDocument(counter);
        if (ifMatchEtag is "\"seed\"")
        {
            var created = await store
                .CreateAsync(document, CounterDocument.DocumentId, counter.PartitionKey, cancellationToken)
                .ConfigureAwait(false);
            return CosmosDocumentMapper.ToRecord(created.Resource, created.ETag);
        }

        var replaced = await store
            .ReplaceAsync(
                document,
                CounterDocument.DocumentId,
                counter.PartitionKey,
                ifMatchEtag,
                cancellationToken)
            .ConfigureAwait(false);
        return CosmosDocumentMapper.ToRecord(replaced.Resource, replaced.ETag);
    }

    public async Task<RedemptionRecord?> GetByOrderIdAsync(
        string partitionKey,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await store
            .ReadAsync<RedemptionDocument>(orderId, partitionKey, cancellationToken)
            .ConfigureAwait(false);

        return result is null ? null : CosmosDocumentMapper.ToRecord(result.Resource, result.ETag);
    }

    public async Task<RedemptionRecord?> FindByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<RedemptionDocument>(
                "SELECT * FROM c WHERE c.type = @type AND c.orderId = @orderId",
                new Dictionary<string, object>
                {
                    ["@type"] = RedemptionDocument.DocumentType,
                    ["@orderId"] = orderId,
                },
                partitionKey: null,
                cancellationToken)
            .ConfigureAwait(false);

        var match = results.FirstOrDefault();
        return match is null ? null : CosmosDocumentMapper.ToRecord(match.Resource, match.ETag);
    }

    public async Task<RedemptionRecord> InsertRedemptionAsync(
        RedemptionRecord redemption,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        var document = CosmosDocumentMapper.ToDocument(redemption);
        var created = await store
            .CreateAsync(document, redemption.OrderId, redemption.PartitionKey, cancellationToken)
            .ConfigureAwait(false);
        return CosmosDocumentMapper.ToRecord(created.Resource, created.ETag);
    }

    public async Task<RedemptionRecord> ReplaceRedemptionAsync(
        RedemptionRecord redemption,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);
        var document = CosmosDocumentMapper.ToDocument(redemption);
        var replaced = await store
            .ReplaceAsync(
                document,
                redemption.OrderId,
                redemption.PartitionKey,
                ifMatchEtag,
                cancellationToken)
            .ConfigureAwait(false);
        return CosmosDocumentMapper.ToRecord(replaced.Resource, replaced.ETag);
    }

    public async Task<int> CountConfirmedByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<RedemptionDocument>(
                "SELECT * FROM c WHERE c.type = @type AND c.customerId = @customerId AND c.state = @state",
                new Dictionary<string, object>
                {
                    ["@type"] = RedemptionDocument.DocumentType,
                    ["@customerId"] = customerId,
                    ["@state"] = nameof(RedemptionState.Confirmed),
                },
                partitionKey,
                cancellationToken)
            .ConfigureAwait(false);

        return results.Count;
    }

    public async Task<int> CountConsumingByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<RedemptionDocument>(
                "SELECT * FROM c WHERE c.type = @type AND c.customerId = @customerId AND ARRAY_CONTAINS(@states, c.state)",
                new Dictionary<string, object>
                {
                    ["@type"] = RedemptionDocument.DocumentType,
                    ["@customerId"] = customerId,
                    ["@states"] = new[]
                    {
                        nameof(RedemptionState.Reserved),
                        nameof(RedemptionState.Confirmed),
                    },
                },
                partitionKey,
                cancellationToken)
            .ConfigureAwait(false);

        return results.Count;
    }

    public async Task ExpireStaleReservationsAsync(
        string partitionKey,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var stale = await store
            .QueryAsync<RedemptionDocument>(
                "SELECT * FROM c WHERE c.type = @type AND c.state = @state AND c.ttlExpiresAt <= @asOf",
                new Dictionary<string, object>
                {
                    ["@type"] = RedemptionDocument.DocumentType,
                    ["@state"] = nameof(RedemptionState.Reserved),
                    ["@asOf"] = asOf,
                },
                partitionKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var item in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expired = CosmosDocumentMapper.ToRecord(item.Resource, item.ETag) with
            {
                State = RedemptionState.Expired,
                TtlExpiresAt = null,
            };

            try
            {
                await ReplaceRedemptionAsync(expired, item.ETag, cancellationToken).ConfigureAwait(false);
            }
            catch (PreconditionFailedException)
            {
                // Lost the race to another expire/confirm — ignore.
            }
        }

        var counter = await GetCounterAsync(partitionKey, cancellationToken).ConfigureAwait(false);
        if (counter is null)
        {
            return;
        }

        var updated = counter with
        {
            ActiveReservations = Math.Max(0, counter.ActiveReservations - stale.Count),
        };

        try
        {
            await UpsertCounterAsync(updated, counter.ETag, cancellationToken).ConfigureAwait(false);
        }
        catch (PreconditionFailedException)
        {
            logger.LogDebug("Counter ETag raced while expiring stale reservations for {PartitionKey}", partitionKey);
        }
    }

    public async Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReserveAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        ArgumentNullException.ThrowIfNull(expectedCounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterIfMatchEtag);

        // AC-4.5: single-partition transactional batch — insert redemption + counter CAS.
        var redemptionDocument = CosmosDocumentMapper.ToDocument(redemption);
        var counterDocument = CosmosDocumentMapper.ToDocument(expectedCounter);

        var operations = new List<CosmosBatchOperation>
        {
            new(CosmosBatchOperationKind.Create, redemption.OrderId, redemptionDocument),
            new(
                CosmosBatchOperationKind.Replace,
                CounterDocument.DocumentId,
                counterDocument,
                counterIfMatchEtag),
        };

        var batch = await store
            .ExecuteTransactionalBatchAsync(redemption.PartitionKey, operations, cancellationToken)
            .ConfigureAwait(false);

        if (!batch.IsSuccessStatusCode)
        {
            // Unique-key / create conflict on the redemption → InvalidOperationException (AC-4.6).
            // Counter ETag mismatch → PreconditionFailedException for redeemer retry (AC-4.5).
            if (batch.Operations.Count > 0
                && batch.Operations[0].StatusCode is (int)System.Net.HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException(
                    $"Redemption for order '{redemption.OrderId}' already exists.");
            }

            CosmosExceptionMapper.ThrowForBatchFailure(batch);
        }

        var redemptionEtag = batch.Operations[0].ETag
            ?? throw new InvalidOperationException("Reserve batch did not return a redemption ETag.");
        var counterEtag = batch.Operations[1].ETag
            ?? throw new InvalidOperationException("Reserve batch did not return a counter ETag.");

        return (
            redemption with { ETag = redemptionEtag },
            expectedCounter with { ETag = counterEtag });
    }

    public async Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryConfirmAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default) =>
        await TryTransitionAsync(
                redemption,
                expectedCounter,
                redemptionIfMatchEtag,
                counterIfMatchEtag,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReleaseAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default) =>
        await TryTransitionAsync(
                redemption,
                expectedCounter,
                redemptionIfMatchEtag,
                counterIfMatchEtag,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryTransitionAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        ArgumentNullException.ThrowIfNull(expectedCounter);
        ArgumentException.ThrowIfNullOrWhiteSpace(redemptionIfMatchEtag);
        ArgumentException.ThrowIfNullOrWhiteSpace(counterIfMatchEtag);

        var redemptionDocument = CosmosDocumentMapper.ToDocument(redemption);
        var counterDocument = CosmosDocumentMapper.ToDocument(expectedCounter);

        var operations = new List<CosmosBatchOperation>
        {
            new(
                CosmosBatchOperationKind.Replace,
                redemption.OrderId,
                redemptionDocument,
                redemptionIfMatchEtag),
            new(
                CosmosBatchOperationKind.Replace,
                CounterDocument.DocumentId,
                counterDocument,
                counterIfMatchEtag),
        };

        var batch = await store
            .ExecuteTransactionalBatchAsync(redemption.PartitionKey, operations, cancellationToken)
            .ConfigureAwait(false);

        CosmosExceptionMapper.ThrowForBatchFailure(batch);

        return (
            redemption with { ETag = batch.Operations[0].ETag! },
            expectedCounter with { ETag = batch.Operations[1].ETag! });
    }
}
