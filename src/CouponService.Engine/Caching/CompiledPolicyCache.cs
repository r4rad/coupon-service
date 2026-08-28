using CouponService.Domain;
using CouponService.Engine.Compilation;

namespace CouponService.Engine.Caching;

public sealed class CompiledPolicyCache
{
    private readonly object _gate = new();
    private readonly CompiledPolicyCacheOptions _options;
    private readonly IClock _clock;
    private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();

    public CompiledPolicyCache(IClock clock, CompiledPolicyCacheOptions? options = null)
    {
        _clock = clock;
        _options = options ?? CompiledPolicyCacheOptions.Default;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public CompiledPolicyHandle GetOrAdd(string policyDocumentJson, Func<CompiledCondition> compile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyDocumentJson);
        ArgumentNullException.ThrowIfNull(compile);

        var contentHash = PolicyContentHasher.ComputeHash(policyDocumentJson);

        lock (_gate)
        {
            PurgeExpired();

            if (_entries.TryGetValue(contentHash, out var cached))
            {
                Touch(contentHash, cached);
                return new CompiledPolicyHandle(contentHash, cached.Condition);
            }
        }

        var compiled = compile();

        lock (_gate)
        {
            PurgeExpired();

            if (_entries.TryGetValue(contentHash, out var cached))
            {
                Touch(contentHash, cached);
                return new CompiledPolicyHandle(contentHash, cached.Condition);
            }

            EvictIfNeeded();
            AddEntry(contentHash, compiled);
            return new CompiledPolicyHandle(contentHash, compiled);
        }
    }

    public bool TryGet(string contentHash, out CompiledPolicyHandle handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        lock (_gate)
        {
            PurgeExpired();

            if (_entries.TryGetValue(contentHash, out var cached))
            {
                Touch(contentHash, cached);
                handle = new CompiledPolicyHandle(contentHash, cached.Condition);
                return true;
            }
        }

        handle = null!;
        return false;
    }

    private void PurgeExpired()
    {
        var cutoff = _clock.UtcNow - _options.SlidingExpiration;
        var expiredKeys = _entries
            .Where(entry => entry.Value.LastAccessedAt < cutoff)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var key in expiredKeys)
        {
            RemoveEntry(key);
        }
    }

    private void EvictIfNeeded()
    {
        while (_entries.Count >= _options.MaxEntries && _lru.First is not null)
        {
            RemoveEntry(_lru.First.Value);
        }
    }

    private void AddEntry(string contentHash, CompiledCondition condition)
    {
        var entry = new CacheEntry(condition, _clock.UtcNow);
        _entries[contentHash] = entry;
        _lru.AddLast(contentHash);
    }

    private void Touch(string contentHash, CacheEntry entry)
    {
        entry.LastAccessedAt = _clock.UtcNow;
        _lru.Remove(contentHash);
        _lru.AddLast(contentHash);
    }

    private void RemoveEntry(string contentHash)
    {
        _entries.Remove(contentHash);
        _lru.Remove(contentHash);
    }

    private sealed class CacheEntry(CompiledCondition condition, DateTimeOffset accessedAt)
    {
        internal CompiledCondition Condition { get; } = condition;

        internal DateTimeOffset LastAccessedAt { get; set; } = accessedAt;
    }
}
