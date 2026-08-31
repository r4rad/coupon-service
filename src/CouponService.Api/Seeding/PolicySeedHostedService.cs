using Microsoft.Extensions.Options;

namespace CouponService.Api.Seeding;

/// <summary>
/// Seeds at startup so a completed deployment is a seeded deployment (AC-9.5), with no admin
/// token, gateway hop or pipeline step in the path.
/// </summary>
public sealed class PolicySeedHostedService(
    IOptions<PolicySeedOptions> options,
    PolicySeeder seeder,
    PolicySeedState state,
    ILogger<PolicySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Policy seeding disabled; leaving the policy store untouched.");
            state.MarkDisabled();
            return;
        }

        try
        {
            var report = await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            state.MarkSucceeded(report);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Readiness reports the failure rather than crashing the host: a crash loop hides the
            // reason, while an unhealthy probe carries it to the deployment that caused it.
            logger.LogError(ex, "Policy seeding failed.");
            state.MarkFailed(ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
