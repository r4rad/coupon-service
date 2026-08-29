using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace CouponService.Infrastructure.Cosmos;

public sealed class CosmosItemStore(Container container, ILogger logger) : ICosmosItemStore
{
    private readonly string _containerId = container.Id;

    public async Task<CosmosItemResult<T>?> ReadAsync<T>(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container
                .ReadItemAsync<T>(id, new PartitionKey(partitionKey), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            CosmosRequestChargeLogging.Log(logger, "ReadItem", _containerId, response.RequestCharge);
            return new CosmosItemResult<T>(response.Resource, response.ETag, response.RequestCharge);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            CosmosRequestChargeLogging.Log(logger, "ReadItem", _containerId, ex.RequestCharge);
            return null;
        }
        catch (CosmosException ex)
        {
            CosmosRequestChargeLogging.Log(logger, "ReadItem", _containerId, ex.RequestCharge);
            throw CosmosExceptionMapper.Map(ex);
        }
    }

    public async Task<CosmosItemResult<T>> CreateAsync<T>(
        T item,
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container
                .CreateItemAsync(item, new PartitionKey(partitionKey), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            CosmosRequestChargeLogging.Log(logger, "CreateItem", _containerId, response.RequestCharge);
            return new CosmosItemResult<T>(response.Resource, response.ETag, response.RequestCharge);
        }
        catch (CosmosException ex)
        {
            CosmosRequestChargeLogging.Log(logger, "CreateItem", _containerId, ex.RequestCharge);
            throw CosmosExceptionMapper.Map(ex);
        }
    }

    public async Task<CosmosItemResult<T>> ReplaceAsync<T>(
        T item,
        string id,
        string partitionKey,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container
                .ReplaceItemAsync(
                    item,
                    id,
                    new PartitionKey(partitionKey),
                    new ItemRequestOptions { IfMatchEtag = ifMatchEtag },
                    cancellationToken)
                .ConfigureAwait(false);

            CosmosRequestChargeLogging.Log(logger, "ReplaceItem", _containerId, response.RequestCharge);
            return new CosmosItemResult<T>(response.Resource, response.ETag, response.RequestCharge);
        }
        catch (CosmosException ex)
        {
            CosmosRequestChargeLogging.Log(logger, "ReplaceItem", _containerId, ex.RequestCharge);
            throw CosmosExceptionMapper.Map(ex);
        }
    }

    public async Task<IReadOnlyList<CosmosItemResult<T>>> QueryAsync<T>(
        string sql,
        IReadOnlyDictionary<string, object> parameters,
        string? partitionKey,
        CancellationToken cancellationToken = default)
    {
        var definition = new QueryDefinition(sql);
        foreach (var (name, value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        var requestOptions = partitionKey is null
            ? null
            : new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) };

        var results = new List<CosmosItemResult<T>>();
        var totalCharge = 0d;

        using var iterator = container.GetItemQueryIterator<T>(definition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            totalCharge += page.RequestCharge;
            foreach (var item in page)
            {
                // Query feed does not expose per-item ETags on the resource alone; empty etag for query hits.
                results.Add(new CosmosItemResult<T>(item, string.Empty, page.RequestCharge));
            }
        }

        CosmosRequestChargeLogging.Log(logger, "Query", _containerId, totalCharge);
        return results;
    }

    public async Task<CosmosBatchResult> ExecuteTransactionalBatchAsync(
        string partitionKey,
        IReadOnlyList<CosmosBatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        var batch = container.CreateTransactionalBatch(new PartitionKey(partitionKey));
        foreach (var operation in operations)
        {
            switch (operation.Kind)
            {
                case CosmosBatchOperationKind.Create:
                    batch.CreateItem(operation.Item);
                    break;
                case CosmosBatchOperationKind.Replace:
                    batch.ReplaceItem(
                        operation.Id,
                        operation.Item,
                        new TransactionalBatchItemRequestOptions { IfMatchEtag = operation.IfMatchEtag });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operations), operation.Kind, null);
            }
        }

        var response = await batch.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        CosmosRequestChargeLogging.Log(logger, "TransactionalBatch", _containerId, response.RequestCharge);

        var operationResults = new List<CosmosBatchOperationResult>(response.Count);
        for (var i = 0; i < response.Count; i++)
        {
            var result = response[i];
            operationResults.Add(new CosmosBatchOperationResult(
                (int)result.StatusCode,
                result.ETag,
                result.ResourceStream is null ? null : ReadBody(result)));
        }

        return new CosmosBatchResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            response.RequestCharge,
            operationResults);
    }

    private static string ReadBody(TransactionalBatchOperationResult result)
    {
        using var reader = new StreamReader(result.ResourceStream!);
        return reader.ReadToEnd();
    }
}
