using CouponService.Application.Pricing;

namespace CouponService.Application.Policies;

public sealed record EvaluatedPolicyCandidate(
    PolicyRecord Policy,
    PolicyDecision Decision);
