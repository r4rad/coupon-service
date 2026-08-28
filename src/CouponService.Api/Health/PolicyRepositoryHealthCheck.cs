using CouponService.Application.Policies;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CouponService.Api.Health;

public sealed class PolicyRepositoryHealthCheck(IPolicyRepository policies) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = await policies.ListAutomaticAsync(cancellationToken).ConfigureAwait(false);
        return HealthCheckResult.Healthy("Policy repository is reachable.");
    }
}
