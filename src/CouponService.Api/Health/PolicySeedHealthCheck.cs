using CouponService.Api.Seeding;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CouponService.Api.Health;

/// <summary>
/// Makes the startup seed verifiable from an anonymous readiness probe, which is how CD confirms
/// AC-9.5 without holding an admin credential.
/// </summary>
public sealed class PolicySeedHealthCheck(PolicySeedState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var (status, report, error) = state.Read();

        var result = status switch
        {
            PolicySeedStatus.Disabled => HealthCheckResult.Healthy("Policy seeding is disabled."),
            PolicySeedStatus.Succeeded => HealthCheckResult.Healthy(
                $"Seeded {report!.Total} policies ({report.Created} created, {report.Updated} updated, {report.Unchanged} unchanged)."),
            PolicySeedStatus.Failed => HealthCheckResult.Unhealthy($"Policy seeding failed: {error}"),
            _ => HealthCheckResult.Unhealthy("Policy seeding has not completed."),
        };

        return Task.FromResult(result);
    }
}
