using CouponService.Application.Pricing;

namespace CouponService.Application.Policies;

public interface IPolicyCandidateResolver
{
    PolicyDecision? Resolve(IReadOnlyList<EvaluatedPolicyCandidate> candidates);
}
