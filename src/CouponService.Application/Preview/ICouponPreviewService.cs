using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Preview;

public interface ICouponPreviewService
{
    Task<PreviewResult> PreviewAsync(
        string code,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default);
}
