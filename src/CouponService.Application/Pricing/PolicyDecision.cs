using CouponService.Domain;
using CouponService.Engine.Evaluation;

namespace CouponService.Application.Pricing;

public sealed record PolicyDecision(
    CouponStatus Status,
    RejectionReason? Reason,
    DiscountPlan? Plan,
    NearMissHint? Hint,
    EvaluationTrace? Trace,
    string? PolicyContentHash)
{
    public static PolicyDecision Applied(DiscountPlan plan, string policyContentHash) =>
        new(CouponStatus.Applied, null, plan, null, null, policyContentHash);

    public static PolicyDecision Rejected(
        RejectionReason reason,
        string? policyContentHash,
        NearMissHint? hint = null,
        EvaluationTrace? trace = null) =>
        new(CouponStatus.Rejected, reason, null, hint, trace, policyContentHash);

    public static PolicyDecision NotFound() =>
        new(CouponStatus.Rejected, RejectionReason.NotFound, null, null, null, null);
}
