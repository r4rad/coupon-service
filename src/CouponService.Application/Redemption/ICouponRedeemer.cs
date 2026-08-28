namespace CouponService.Application.Redemption;

public interface ICouponRedeemer
{
    Task<ReservationResult> ReserveAsync(
        string code,
        string orderId,
        string customerId,
        CancellationToken cancellationToken = default);

    Task<RedemptionResult> ConfirmAsync(string orderId, CancellationToken cancellationToken = default);

    Task<RedemptionResult> ReleaseAsync(
        string orderId,
        string reason,
        CancellationToken cancellationToken = default);
}
