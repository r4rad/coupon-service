using System.Net;
using System.Text.Json;
using CouponService.Application.Redemption;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CouponService.Infrastructure.Cosmos;

/// <summary>
/// In-process Cosmos stand-in for unit tests: transactional batch, ETag CAS, and unique id-per-partition (P-9 / AC-4.5).
/// </summary>
public sealed class InMemoryCosmosItemStore : ICosmosItemStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, StoredItem>> _partitions = new(StringComparer.Ordinal);
    private readonly ILogger _logger;
    private int _etagSequence;

    public InMemoryCosmosItemStore(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public string ContainerId { get; init; } = "test";

    public double NextRequestCharge { get; set; } = 1.0;

    public double LastRequestCharge { get; private set; } = 1.0;

    public Task<CosmosItemResult<T>?> ReadAsync<T>(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Charge("ReadItem");
            if (!_partitions.TryGetValue(partitionKey, out var items)
                || !items.TryGetValue(id, out var stored))
            {
                return Task.FromResult<CosmosItemResult<T>?>(null);
            }

            return Task.FromResult<CosmosItemResult<T>?>(
                new CosmosItemResult<T>(Deserialize<T>(stored.Json), stored.ETag, LastRequestCharge));
        }
    }

    public Task<CosmosItemResult<T>> CreateAsync<T>(
        T item,
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            Charge("CreateItem");
            var bucket = GetOrCreatePartition(partitionKey);
            if (bucket.ContainsKey(id) || HasOrderIdConflict(bucket, item))
            {
                throw new InvalidOperationException($"Item '{id}' already exists in partition '{partitionKey}'.");
            }

            var etag = NextEtag();
            bucket[id] = Store(item, etag);
            return Task.FromResult(new CosmosItemResult<T>(item, etag, LastRequestCharge));
        }
    }

    public Task<CosmosItemResult<T>> ReplaceAsync<T>(
        T item,
        string id,
        string partitionKey,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate)
        {
            Charge("ReplaceItem");
            if (!_partitions.TryGetValue(partitionKey, out var bucket)
                || !bucket.TryGetValue(id, out var existing))
            {
                throw new KeyNotFoundException($"Item '{id}' was not found.");
            }

            if (!string.Equals(existing.ETag, ifMatchEtag, StringComparison.Ordinal))
            {
                throw new PreconditionFailedException();
            }

            var etag = NextEtag();
            bucket[id] = Store(item, etag);
            return Task.FromResult(new CosmosItemResult<T>(item, etag, LastRequestCharge));
        }
    }

    public Task<IReadOnlyList<CosmosItemResult<T>>> QueryAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        string? partitionKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Charge("Query");
            IEnumerable<StoredItem> source = partitionKey is null
                ? _partitions.Values.SelectMany(v => v.Values)
                : _partitions.TryGetValue(partitionKey, out var bucket) ? bucket.Values : [];

            var results = new List<CosmosItemResult<T>>();
            foreach (var stored in source)
            {
                var resource = Deserialize<T>(stored.Json);
                if (!QueryPredicate.Matches(resource, sql, parameters))
                {
                    continue;
                }

                results.Add(new CosmosItemResult<T>(resource, stored.ETag, LastRequestCharge));
            }

            return Task.FromResult<IReadOnlyList<CosmosItemResult<T>>>(results);
        }
    }

    public Task<CosmosBatchResult> ExecuteTransactionalBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Charge("TransactionalBatch");
            var bucket = GetOrCreatePartition(partitionKey);
            var staging = new Dictionary<string, StoredItem>(bucket, StringComparer.Ordinal);
            var opResults = new List<CosmosBatchOperationResult>(operations.Count);

            foreach (var operation in operations)
            {
                if (operation.Kind is CosmosBatchOperationKind.Create)
                {
                    if (staging.ContainsKey(operation.Id) || HasOrderIdConflict(staging, operation.Item))
                    {
                        return Task.FromResult(Fail(HttpStatusCode.Conflict, opResults, operations.Count));
                    }

                    var etag = NextEtag();
                    staging[operation.Id] = Store(operation.Item, etag);
                    opResults.Add(new CosmosBatchOperationResult(
                        (int)HttpStatusCode.Created,
                        etag,
                        Serialize(operation.Item)));
                    continue;
                }

                if (operation.Kind is CosmosBatchOperationKind.Replace)
                {
                    if (!staging.TryGetValue(operation.Id, out var existing))
                    {
                        // First counter write uses Replace with seed only when absent → treat as create.
                        if (operation.IfMatchEtag is "\"seed\"")
                        {
                            var etag = NextEtag();
                            staging[operation.Id] = Store(operation.Item, etag);
                            opResults.Add(new CosmosBatchOperationResult(
                                (int)HttpStatusCode.OK,
                                etag,
                                Serialize(operation.Item)));
                            continue;
                        }

                        return Task.FromResult(Fail(HttpStatusCode.NotFound, opResults, operations.Count));
                    }

                    if (operation.IfMatchEtag is "\"seed\""
                        || (operation.IfMatchEtag is not null
                            && !string.Equals(existing.ETag, operation.IfMatchEtag, StringComparison.Ordinal)))
                    {
                        return Task.FromResult(Fail(HttpStatusCode.PreconditionFailed, opResults, operations.Count));
                    }

                    var replaceEtag = NextEtag();
                    staging[operation.Id] = Store(operation.Item, replaceEtag);
                    opResults.Add(new CosmosBatchOperationResult(
                        (int)HttpStatusCode.OK,
                        replaceEtag,
                        Serialize(operation.Item)));
                    continue;
                }

                throw new ArgumentOutOfRangeException(nameof(operations), operation.Kind, null);
            }

            _partitions[partitionKey] = staging;
            return Task.FromResult(new CosmosBatchResult(
                true,
                (int)HttpStatusCode.OK,
                LastRequestCharge,
                opResults));
        }
    }

    private CosmosBatchResult Fail(
        HttpStatusCode status,
        List<CosmosBatchOperationResult> partial,
        int totalOps)
    {
        while (partial.Count < totalOps)
        {
            partial.Add(new CosmosBatchOperationResult((int)HttpStatusCode.FailedDependency, null, null));
        }

        partial[^1] = new CosmosBatchOperationResult((int)status, null, null);
        return new CosmosBatchResult(false, (int)status, LastRequestCharge, partial);
    }

    private void Charge(string operation)
    {
        LastRequestCharge = NextRequestCharge;
        CosmosRequestChargeLogging.Log(_logger, operation, ContainerId, LastRequestCharge);
    }

    private Dictionary<string, StoredItem> GetOrCreatePartition(string partitionKey)
    {
        if (!_partitions.TryGetValue(partitionKey, out var bucket))
        {
            bucket = new Dictionary<string, StoredItem>(StringComparer.Ordinal);
            _partitions[partitionKey] = bucket;
        }

        return bucket;
    }

    private string NextEtag()
    {
        _etagSequence++;
        return $"\"{_etagSequence}\"";
    }

    private static bool HasOrderIdConflict(IReadOnlyDictionary<string, StoredItem> bucket, object item)
    {
        if (item is not RedemptionDocument redemption)
        {
            return false;
        }

        return bucket.Values.Any(existing =>
            existing.OrderId is not null
            && string.Equals(existing.OrderId, redemption.OrderId, StringComparison.Ordinal));
    }

    private static StoredItem Store(object item, string etag) =>
        new(Serialize(item), etag, item is RedemptionDocument r ? r.OrderId : null);

    private static string Serialize(object item) =>
        JsonSerializer.Serialize(item, item.GetType(), CosmosJsonContext.Default);

    private static T Deserialize<T>(string json) =>
        (T)JsonSerializer.Deserialize(json, typeof(T), CosmosJsonContext.Default)!;

    private sealed record StoredItem(string Json, string ETag, string? OrderId);
}

internal static class QueryPredicate
{
    internal static bool Matches<T>(T resource, string sql, IReadOnlyDictionary<string, object> parameters)
    {
        if (resource is RedemptionDocument redemption)
        {
            if (ParamMismatch(sql, "c.type = @type", parameters, "@type", redemption.Type))
            {
                return false;
            }

            if (ParamMismatch(sql, "c.orderId = @orderId", parameters, "@orderId", redemption.OrderId))
            {
                return false;
            }

            if (ParamMismatch(sql, "c.customerId = @customerId", parameters, "@customerId", redemption.CustomerId))
            {
                return false;
            }

            if (ParamMismatch(sql, "c.state = @state", parameters, "@state", redemption.State))
            {
                return false;
            }

            if (sql.Contains("ARRAY_CONTAINS(@states, c.state)", StringComparison.Ordinal)
                && parameters.TryGetValue("@states", out var statesObj)
                && statesObj is IEnumerable<string> states
                && !states.Contains(redemption.State, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (sql.Contains("c.ttlExpiresAt <= @asOf", StringComparison.Ordinal)
                && parameters.TryGetValue("@asOf", out var asOfObj)
                && asOfObj is DateTimeOffset asOf
                && (redemption.TtlExpiresAt is null || redemption.TtlExpiresAt > asOf))
            {
                return false;
            }

            return true;
        }

        if (resource is PolicyDocument policy)
        {
            if (ParamMismatch(sql, "c.trigger = @trigger", parameters, "@trigger", policy.Trigger))
            {
                return false;
            }

            if (ParamMismatch(sql, "c.policyId = @policyId", parameters, "@policyId", policy.PolicyId))
            {
                return false;
            }

            if (sql.Contains("ARRAY_CONTAINS(@statuses, c.status)", StringComparison.Ordinal)
                && parameters.TryGetValue("@statuses", out var statusesObj)
                && statusesObj is IEnumerable<string> statuses
                && !statuses.Contains(policy.Status, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        return true;
    }

    private static bool ParamMismatch(
        string sql,
        string fragment,
        IReadOnlyDictionary<string, object> parameters,
        string name,
        string actual) =>
        sql.Contains(fragment, StringComparison.Ordinal)
        && parameters.TryGetValue(name, out var expected)
        && !string.Equals(actual, expected?.ToString(), StringComparison.OrdinalIgnoreCase);
}
