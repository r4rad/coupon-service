using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Engine;

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(
        PolicyRecord policy,
        Cart cart,
        CustomerContext customer,
        bool captureFullTrace = false,
        CancellationToken cancellationToken = default);
}
