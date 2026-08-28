using CouponService.Application.Policies;
using CouponService.Application.Redemption;

namespace CouponService.Infrastructure.InMemory;

public sealed class InMemoryPolicyRepository : IPolicyRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private int _etagSequence;

    public int WriteCount { get; private set; }

    public Task<PolicyRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        GetByPartitionKeyAsync(code, cancellationToken);

    public Task<PolicyRecord?> GetByPartitionKeyAsync(
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _policies.TryGetValue(partitionKey, out var stored) ? stored.ToRecord() : null);
        }
    }

    public Task<PolicyRecord> CreateAsync(PolicyRecord policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        lock (_gate)
        {
            if (_policies.ContainsKey(policy.PartitionKey))
            {
                throw new InvalidOperationException($"Policy '{policy.PartitionKey}' already exists.");
            }

            var etag = NextEtag();
            var stored = StoredPolicy.FromRecord(policy with { ETag = etag }, etag);
            _policies[policy.PartitionKey] = stored;
            WriteCount++;
            return Task.FromResult(stored.ToRecord());
        }
    }

    public Task<PolicyRecord> ReplaceAsync(
        PolicyRecord policy,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        lock (_gate)
        {
            if (!_policies.TryGetValue(policy.PartitionKey, out var existing))
            {
                throw new KeyNotFoundException($"Policy '{policy.PartitionKey}' was not found.");
            }

            if (!string.Equals(existing.ETag, ifMatchEtag, StringComparison.Ordinal))
            {
                throw new PreconditionFailedException();
            }

            var etag = NextEtag();
            var stored = StoredPolicy.FromRecord(policy with { ETag = etag }, etag);
            _policies[policy.PartitionKey] = stored;
            WriteCount++;
            return Task.FromResult(stored.ToRecord());
        }
    }

    private string NextEtag()
    {
        _etagSequence++;
        return $"\"{_etagSequence}\"";
    }

    private sealed class StoredPolicy(
        string partitionKey,
        string policyId,
        string? code,
        PolicyTrigger trigger,
        string documentJson,
        string etag)
    {
        internal string PartitionKey { get; } = partitionKey;

        internal string ETag { get; } = etag;

        internal static StoredPolicy FromRecord(PolicyRecord record, string etag) =>
            new(record.PartitionKey, record.PolicyId, record.Code, record.Trigger, record.DocumentJson, etag);

        internal PolicyRecord ToRecord() =>
            new(PartitionKey, policyId, code, trigger, documentJson, ETag);
    }
}
