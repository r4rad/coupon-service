using CouponService.Application.Redemption;
using CouponService.Infrastructure.InMemory;

namespace CouponService.UnitTests.Redemption;

internal sealed class FlakyCounterRedemptionRepository : IRedemptionRepository
{
    private readonly InMemoryRedemptionRepository _inner = new();
    private readonly int _failuresBeforeSuccess;

    internal FlakyCounterRedemptionRepository(int failuresBeforeSuccess)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    internal int TryReserveAttempts { get; private set; }

    public Task<UsageCounterRecord?> GetCounterAsync(string partitionKey, CancellationToken cancellationToken = default) =>
        _inner.GetCounterAsync(partitionKey, cancellationToken);

    public Task<UsageCounterRecord> UpsertCounterAsync(
        UsageCounterRecord counter,
        string ifMatchEtag,
        CancellationToken cancellationToken = default) =>
        _inner.UpsertCounterAsync(counter, ifMatchEtag, cancellationToken);

    public Task<RedemptionRecord?> GetByOrderIdAsync(
        string partitionKey,
        string orderId,
        CancellationToken cancellationToken = default) =>
        _inner.GetByOrderIdAsync(partitionKey, orderId, cancellationToken);

    public Task<RedemptionRecord?> FindByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken = default) =>
        _inner.FindByOrderIdAsync(orderId, cancellationToken);

    public Task<RedemptionRecord> InsertRedemptionAsync(
        RedemptionRecord redemption,
        CancellationToken cancellationToken = default) =>
        _inner.InsertRedemptionAsync(redemption, cancellationToken);

    public Task<RedemptionRecord> ReplaceRedemptionAsync(
        RedemptionRecord redemption,
        string ifMatchEtag,
        CancellationToken cancellationToken = default) =>
        _inner.ReplaceRedemptionAsync(redemption, ifMatchEtag, cancellationToken);

    public Task<int> CountConfirmedByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default) =>
        _inner.CountConfirmedByCustomerAsync(partitionKey, customerId, cancellationToken);

    public Task<int> CountConsumingByCustomerAsync(
        string partitionKey,
        string customerId,
        CancellationToken cancellationToken = default) =>
        _inner.CountConsumingByCustomerAsync(partitionKey, customerId, cancellationToken);

    public Task ExpireStaleReservationsAsync(
        string partitionKey,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default) =>
        _inner.ExpireStaleReservationsAsync(partitionKey, asOf, cancellationToken);

    public Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReserveAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default)
    {
        TryReserveAttempts++;
        if (TryReserveAttempts <= _failuresBeforeSuccess)
        {
            throw new PreconditionFailedException();
        }

        return _inner.TryReserveAsync(redemption, expectedCounter, counterIfMatchEtag, cancellationToken);
    }

    public Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryConfirmAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default) =>
        _inner.TryConfirmAsync(
            redemption,
            expectedCounter,
            redemptionIfMatchEtag,
            counterIfMatchEtag,
            cancellationToken);

    public Task<(RedemptionRecord Redemption, UsageCounterRecord Counter)> TryReleaseAsync(
        RedemptionRecord redemption,
        UsageCounterRecord expectedCounter,
        string redemptionIfMatchEtag,
        string counterIfMatchEtag,
        CancellationToken cancellationToken = default) =>
        _inner.TryReleaseAsync(
            redemption,
            expectedCounter,
            redemptionIfMatchEtag,
            counterIfMatchEtag,
            cancellationToken);
}
