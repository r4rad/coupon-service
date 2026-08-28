using CouponService.Application.Engine;
using CouponService.Application.Pricing;
using CouponService.Application.Validation;
using CouponService.Domain;

namespace CouponService.Application.Policies;

public sealed class AutomaticPolicyPreviewService(
    IAutomaticPolicyIndex index,
    IPolicyEngine engine,
    IPolicyCandidateResolver resolver) : IAutomaticPolicyPreviewService
{
    public async Task<PolicyDecision> PreviewWithoutCodeAsync(
        Cart cart,
        CustomerContext customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(customer);

        var policies = await index.GetAutomaticPoliciesAsync(cancellationToken).ConfigureAwait(false);
        var candidates = new List<EvaluatedPolicyCandidate>(policies.Count);

        foreach (var policy in policies)
        {
            var decision = await engine
                .EvaluateAsync(policy, cart, customer, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            candidates.Add(new EvaluatedPolicyCandidate(policy, decision));
        }

        return resolver.Resolve(candidates) ?? PolicyDecision.NotFound();
    }
}
