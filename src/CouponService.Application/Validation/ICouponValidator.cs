using CouponService.Application.Pricing;
using CouponService.Domain;

namespace CouponService.Application.Validation;

public interface ICouponValidator
{
    Task<PolicyDecision> ValidateAsync(
        string code,
        Cart cart,
        CustomerContext customer,
        bool captureFullTrace = false,
        CancellationToken cancellationToken = default);
}
