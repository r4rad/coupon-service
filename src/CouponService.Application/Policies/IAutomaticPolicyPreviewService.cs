using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Policies;

public interface IAutomaticPolicyPreviewService
{
    Task<PolicyDecision> PreviewWithoutCodeAsync(
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default);
}
