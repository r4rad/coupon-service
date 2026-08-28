namespace CouponService.Application.Policies;

public interface IPolicyRepository
{
    Task<PolicyRecord?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<PolicyRecord?> GetByPartitionKeyAsync(string partitionKey, CancellationToken cancellationToken = default);

    Task<PolicyRecord> CreateAsync(PolicyRecord policy, CancellationToken cancellationToken = default);

    Task<PolicyRecord> ReplaceAsync(
        PolicyRecord policy,
        string ifMatchEtag,
        CancellationToken cancellationToken = default);
}
