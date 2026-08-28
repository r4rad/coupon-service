using CouponService.Domain;

namespace CouponService.Application.Policies;

public sealed class AutomaticPolicyIndex(IPolicyRepository repository, IClock clock) : IAutomaticPolicyIndex
{
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private IReadOnlyList<PolicyRecord>? _cache;
    private DateTimeOffset _cachedAt;

    public async Task<IReadOnlyList<PolicyRecord>> GetAutomaticPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_cache is not null && clock.UtcNow - _cachedAt < CacheTtl)
            {
                return _cache;
            }
        }

        var policies = await repository.ListAutomaticAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
        {
            _cache = policies;
            _cachedAt = clock.UtcNow;
            return _cache;
        }
    }
}
