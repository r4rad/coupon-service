using CouponService.Application.Engine;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Domain;

namespace CouponService.Application.Validation;

public sealed class CouponValidator(IPolicyRepository policies, IPolicyEngine engine) : ICouponValidator
{
    public async Task<PolicyDecision> ValidateAsync(
        string code,
        Cart cart,
        CustomerContext customer,
        bool captureFullTrace = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var policy = await policies.GetByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            return PolicyDecision.NotFound();
        }

        return await engine.EvaluateAsync(policy, cart, customer, captureFullTrace, cancellationToken)
            .ConfigureAwait(false);
    }
}
