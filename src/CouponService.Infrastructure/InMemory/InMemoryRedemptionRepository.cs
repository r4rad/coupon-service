using CouponService.Application.Redemption;

namespace CouponService.Infrastructure.InMemory;

public sealed class InMemoryRedemptionRepository : IRedemptionRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredCounter> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<StoredRedemption>> _redemptions = new(StringComparer.OrdinalIgnoreCase);
    private int _etagSequence;

    public int WriteCount { get; private set; }

    public Task<UsageCounterRecord?> GetCounterAsync(
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _counters.TryGetValue(partitionKey, out var counter) ? counter.ToRecord() : null);
        }
    }

    public Task<UsageCounterRecord> UpsertCounterAsync(
        UsageCounterRecord counter,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(counter);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        lock (_gate)
        {
            if (_counters.TryGetValue(counter.PartitionKey, out var existing)
                && !string.Equals(existing.ETag, ifMatchEtag, StringComparison.Ordinal))
            {
                throw new PreconditionFailedException();
            }

            var etag = NextEtag();
            var stored = new StoredCounter(
                counter.PartitionKey,
                counter.ConfirmedCount,
                counter.ActiveReservations,
                etag);
            _counters[counter.PartitionKey] = stored;
            WriteCount++;
            return Task.FromResult(stored.ToRecord());
        }
    }

    public Task<RedemptionRecord?> GetByOrderIdAsync(
        string partitionKey,
        string orderId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_redemptions.TryGetValue(partitionKey, out var redemptions))
            {
                return Task.FromResult<RedemptionRecord?>(null);
            }

            var match = redemptions.FirstOrDefault(redemption =>
                string.Equals(redemption.OrderId, orderId, StringComparison.Ordinal));

            return Task.FromResult(match?.ToRecord());
        }
    }

    public Task<RedemptionRecord> InsertRedemptionAsync(
        RedemptionRecord redemption,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        lock (_gate)
        {
            var bucket = GetOrCreateRedemptionBucket(redemption.PartitionKey);
            if (bucket.Any(existing =>
                    string.Equals(existing.OrderId, redemption.OrderId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Redemption for order '{redemption.OrderId}' already exists.");
            }

            var etag = NextEtag();
            var stored = StoredRedemption.FromRecord(redemption with { ETag = etag }, etag);
            bucket.Add(stored);
            WriteCount++;
            return Task.FromResult(stored.ToRecord());
        }
    }

    public Task<RedemptionRecord> ReplaceRedemptionAsync(
        RedemptionRecord redemption,
        string ifMatchEtag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);
        ArgumentException.ThrowIfNullOrWhiteSpace(ifMatchEtag);

        lock (_gate)
        {
            var bucket = GetOrCreateRedemptionBucket(redemption.PartitionKey);
            var index = bucket.FindIndex(existing =>
                string.Equals(existing.OrderId, redemption.OrderId, StringComparison.Ordinal));

            if (index < 0)
            {
                throw new KeyNotFoundException($"Redemption '{redemption.OrderId}' was not found.");
            }

            if (!string.Equals(bucket[index].ETag, ifMatchEtag, StringComparison.Ordinal))
            {
                throw new PreconditionFailedException();
            }

            var etag = NextEtag();
            var stored = StoredRedemption.FromRecord(redemption with { ETag = etag }, etag);
            bucket[index] = stored;
            WriteCount++;
            return Task.FromResult(stored.ToRecord());
        }
    }

    public Task<int> CountConfirmedByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_redemptions.TryGetValue(partitionKey, out var redemptions))
            {
                return Task.FromResult(0);
            }

            var count = redemptions.Count(redemption =>
                string.Equals(redemption.CustomerId, customerId, StringComparison.Ordinal)
                && redemption.State is RedemptionState.Confirmed);

            return Task.FromResult(count);
        }
    }

    private List<StoredRedemption> GetOrCreateRedemptionBucket(string partitionKey)
    {
        if (!_redemptions.TryGetValue(partitionKey, out var bucket))
        {
            bucket = [];
            _redemptions[partitionKey] = bucket;
        }

        return bucket;
    }

    private string NextEtag()
    {
        _etagSequence++;
        return $"\"{_etagSequence}\"";
    }

    private sealed record StoredCounter(
        string PartitionKey,
        int ConfirmedCount,
        int ActiveReservations,
        string ETag)
    {
        internal UsageCounterRecord ToRecord() =>
            new(PartitionKey, ConfirmedCount, ActiveReservations, ETag);
    }

    private sealed class StoredRedemption
    {
        internal StoredRedemption(
            string partitionKey,
            string orderId,
            string customerId,
            RedemptionState state,
            decimal discountApplied,
            string policyContentHash,
            string etag,
            DateTimeOffset? ttlExpiresAt)
        {
            PartitionKey = partitionKey;
            OrderId = orderId;
            CustomerId = customerId;
            State = state;
            DiscountApplied = discountApplied;
            PolicyContentHash = policyContentHash;
            ETag = etag;
            TtlExpiresAt = ttlExpiresAt;
        }

        internal string PartitionKey { get; }

        internal string OrderId { get; }

        internal string CustomerId { get; }

        internal RedemptionState State { get; }

        internal decimal DiscountApplied { get; }

        internal string PolicyContentHash { get; }

        internal string ETag { get; }

        internal DateTimeOffset? TtlExpiresAt { get; }

        internal static StoredRedemption FromRecord(RedemptionRecord record, string etag) =>
            new(
                record.PartitionKey,
                record.OrderId,
                record.CustomerId,
                record.State,
                record.DiscountApplied,
                record.PolicyContentHash,
                etag,
                record.TtlExpiresAt);

        internal RedemptionRecord ToRecord() =>
            new(
                PartitionKey,
                OrderId,
                CustomerId,
                State,
                DiscountApplied,
                PolicyContentHash,
                ETag,
                TtlExpiresAt);
    }
}
