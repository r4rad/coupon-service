namespace CouponService.Infrastructure.Cosmos;

public sealed record CosmosItemResult<T>(T Resource, string ETag, double RequestCharge);

public sealed record CosmosBatchResult(
    bool IsSuccessStatusCode,
    int StatusCode,
    double RequestCharge,
    IReadOnlyList<CosmosBatchOperationResult> Operations);

public sealed record CosmosBatchOperationResult(
    int StatusCode,
    string? ETag,
    string? ResourceBody);

public enum CosmosBatchOperationKind
{
    Create,
    Replace,
}

public sealed record CosmosBatchOperation(
    CosmosBatchOperationKind Kind,
    string Id,
    object Item,
    string? IfMatchEtag = null);

/// <summary>
/// Thin store over a single Cosmos container so unit tests can drive CAS / unique-key behaviour without the emulator (P-9).
/// </summary>
public interface ICosmosItemStore
{
    Task<CosmosItemResult<T>?> ReadAsync<T>(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    Task<CosmosItemResult<T>> CreateAsync<T>(
        T item,
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default);

    Task<CosmosItemResult<T>> ReplaceAsync<T>(
        T item,
        string id,
        string partitionKey,
        string ifMatchEtag,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CosmosItemResult<T>>> QueryAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        string? partitionKey,
        CancellationToken cancellationToken = default);

    Task<CosmosBatchResult> ExecuteTransactionalBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation> operations,
        CancellationToken cancellationToken = default);
}
