using CouponService.Application.Policies;

namespace CouponService.Infrastructure.Cosmos;

public sealed class CosmosPolicyRepository(ICosmosItemStore store) : IPolicyRepository
{
    public Task<PolicyRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        GetByPartitionKeyAsync(code, cancellationToken);

    public async Task<PolicyRecord?> GetByPartitionKeyAsync(
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<PolicyDocument>(
                "SELECT * FROM c",
                new Dictionary<string, object>(),
                partitionKey,
                cancellationToken)
            .ConfigureAwait(false);

        var match = results.FirstOrDefault();
        return match is null ? null : CosmosDocumentMapper.ToRecord(match.Resource, ResolveEtag(match));
    }

    public async Task<PolicyRecord?> GetByPolicyIdAsync(
        string policyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        // Cross-partition lookup by policyId (admin read). Prefer point-read when caller knows pk.
        var results = await store
            .QueryAsync<PolicyDocument>(
                "SELECT * FROM c WHERE c.policyId = @policyId",
                new Dictionary<string, object> { ["@policyId"] = policyId },
                partitionKey: null,
                cancellationToken)
            .ConfigureAwait(false);

        var match = results.FirstOrDefault();
        return match is null ? null : CosmosDocumentMapper.ToRecord(match.Resource, ResolveEtag(match));
    }

    public async Task<PolicyRecord> CreateAsync(PolicyRecord policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var document = CosmosDocumentMapper.ToDocument(policy);
        var created = await store
            .CreateAsync(document, policy.PolicyId, policy.PartitionKey, cancellationToken)
            .ConfigureAwait(false);
        return CosmosDocumentMapper.ToRecord(created.Resource, created.ETag);
    }

    public async Task<PolicyRecord> ReplaceAsync(
        PolicyRecord policy,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);
        var document = CosmosDocumentMapper.ToDocument(policy);
        var replaced = await store
            .ReplaceAsync(document, policy.PolicyId, policy.PartitionKey, ifMatchEtag, cancellationToken)
            .ConfigureAwait(false);
        return CosmosDocumentMapper.ToRecord(replaced.Resource, replaced.ETag);
    }

    public async Task<IReadOnlyList<PolicyRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<PolicyDocument>(
                "SELECT * FROM c",
                new Dictionary<string, object>(),
                partitionKey: null,
                cancellationToken)
            .ConfigureAwait(false);

        return results
            .Select(item => CosmosDocumentMapper.ToRecord(item.Resource, ResolveEtag(item)))
            .OrderBy(policy => policy.PolicyId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<PolicyRecord>> ListAutomaticAsync(
        CancellationToken cancellationToken = default)
    {
        var results = await store
            .QueryAsync<PolicyDocument>(
                "SELECT * FROM c WHERE c.trigger = @trigger AND ARRAY_CONTAINS(@statuses, c.status)",
                new Dictionary<string, object>
                {
                    ["@trigger"] = nameof(PolicyTrigger.Automatic),
                    ["@statuses"] = new[]
                    {
                        nameof(PolicyStatus.Active),
                        nameof(PolicyStatus.Shadow),
                    },
                },
                partitionKey: null,
                cancellationToken)
            .ConfigureAwait(false);

        return results
            .Select(item => CosmosDocumentMapper.ToRecord(item.Resource, ResolveEtag(item)))
            .ToArray();
    }

    private static string ResolveEtag(CosmosItemResult<PolicyDocument> item) =>
        string.IsNullOrEmpty(item.ETag) ? item.Resource.CosmosETag ?? string.Empty : item.ETag;
}
