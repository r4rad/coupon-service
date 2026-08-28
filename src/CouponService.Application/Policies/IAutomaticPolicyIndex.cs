namespace CouponService.Application.Policies;

public interface IAutomaticPolicyIndex
{
    Task<IReadOnlyList<PolicyRecord>> GetAutomaticPoliciesAsync(
        CancellationToken cancellationToken = default);
}
