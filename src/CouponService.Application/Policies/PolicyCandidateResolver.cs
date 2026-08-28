using CouponService.Application.Pricing;

namespace CouponService.Application.Policies;

public sealed class PolicyCandidateResolver : IPolicyCandidateResolver
{
    public PolicyDecision? Resolve(IReadOnlyList<EvaluatedPolicyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var applied = candidates
            .Where(candidate =>
                candidate.Decision.Status is CouponStatus.Applied
                && candidate.Decision.Plan is not null)
            .ToList();

        if (applied.Count == 0)
        {
            return null;
        }

        var blockFloor = applied
            .Where(candidate => !PolicyDocumentMetadata.ReadStackable(candidate.Policy.DocumentJson))
            .Select(candidate => PolicyDocumentMetadata.ReadPriority(candidate.Policy.DocumentJson))
            .DefaultIfEmpty(int.MinValue)
            .Max();

        var eligible = blockFloor == int.MinValue
            ? applied
            : applied.Where(candidate =>
                PolicyDocumentMetadata.ReadPriority(candidate.Policy.DocumentJson) >= blockFloor);

        var winner = eligible
            .OrderByDescending(candidate =>
                PolicyDocumentMetadata.ReadPriority(candidate.Policy.DocumentJson))
            .ThenByDescending(candidate => candidate.Decision.Plan!.Total)
            .ThenBy(candidate => candidate.Policy.PolicyId, StringComparer.Ordinal)
            .First();

        return winner.Decision;
    }
}
