using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Redemption;

public interface ICouponRedeemer
{
    Task<ReservationResult> ReserveAsync(
        string code,
        string orderId,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default);

    Task<RedemptionResult> ConfirmAsync(string orderId, CancellationToken cancellationToken = default);

    Task<RedemptionResult> ReleaseAsync(
        string orderId,
        string reason,
        CancellationToken cancellationToken = default);
}
