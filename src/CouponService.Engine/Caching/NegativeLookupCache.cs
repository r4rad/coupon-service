using CouponService.Domain;

namespace CouponService.Engine.Caching;

public sealed class NegativeLookupCache
{
    private readonly object _gate = new();
    private readonly NegativeLookupCacheOptions _options;
    private readonly IClock _clock;
    private readonly Dictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();

    public NegativeLookupCache(IClock clock, NegativeLookupCacheOptions? options = null)
    {
        _clock = clock;
        _options = options ?? NegativeLookupCacheOptions.Default;
    }

    public bool IsBlocked(string lookupKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupKey);

        lock (_gate)
        {
            PurgeExpired();

            if (!_entries.TryGetValue(lookupKey, out var expiresAt))
            {
                return false;
            }

            if (_clock.UtcNow >= expiresAt)
            {
                RemoveEntry(lookupKey);
                return false;
            }

            Touch(lookupKey);
            return true;
        }
    }

    public void RememberFailure(string lookupKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupKey);

        lock (_gate)
        {
            PurgeExpired();

            if (_entries.ContainsKey(lookupKey))
            {
                RemoveEntry(lookupKey);
            }

            while (_entries.Count >= _options.MaxEntries && _lru.First is not null)
            {
                RemoveEntry(_lru.First.Value);
            }

            _entries[lookupKey] = _clock.UtcNow.Add(_options.Ttl);
            _lru.AddLast(lookupKey);
        }
    }

    private void PurgeExpired()
    {
        var now = _clock.UtcNow;
        var expiredKeys = _entries
            .Where(entry => now >= entry.Value)
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var key in expiredKeys)
        {
            RemoveEntry(key);
        }
    }

    private void Touch(string lookupKey)
    {
        _lru.Remove(lookupKey);
        _lru.AddLast(lookupKey);
    }

    private void RemoveEntry(string lookupKey)
    {
        _entries.Remove(lookupKey);
        _lru.Remove(lookupKey);
    }
}
