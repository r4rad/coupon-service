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

    Task<RedemptionRecord?> FindByOrderIdAsync(
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

    Task<int> CountConsumingByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default);

    Task ExpireStaleReservationsAsync(
        string partitionKey,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReserveAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default);

    Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryConfirmAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default);

    Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReleaseAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default);
}
