using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Preview;

public sealed class CouponPreviewService(
    ICouponValidator validator,
    IPriceCalculator calculator) : ICouponPreviewService
{
    public async Task<PreviewResult> PreviewAsync(
        string code,
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default)
    {
        var decision = await validator.ValidateAsync(code, cart, customer, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var breakdown = calculator.Calculate(cart, decision);
        return new PreviewResult(decision, breakdown);
    }
}
