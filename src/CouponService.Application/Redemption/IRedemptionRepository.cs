namespace CouponService.Application.Redemption;

public interface IRedemptionRepository
{
    Task<UsageCounterRecord?> GetCounterAsync(string partitionKey, CancellationToken cancellationToken = default);

    Task<UsageCounterRecord> UpsertCounterAsync(
        UsageCounterRecord counter,
        string ifMatchEtag,
        CancellationToken cancellationToken = default);

    Task<RedemptionRecord?> GetByOrderIdAsync(
        string partitionKey,
        string orderId,
        CancellationToken cancellationToken = default);

    Task<RedemptionRecord> InsertRedemptionAsync(
        RedemptionRecord redemption,
        CancellationToken cancellationToken = default);

    Task<RedemptionRecord> ReplaceRedemptionAsync(
        RedemptionRecord redemption,
        string ifMatchEtag,
        CancellationToken cancellationToken = default);

    Task<int> CountConfirmedByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default);
}
