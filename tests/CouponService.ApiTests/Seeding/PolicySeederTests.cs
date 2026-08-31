using CouponService.Api.Health;
using CouponService.Api.Seeding;
using CouponService.Application.Policies;
using CouponService.Engine.Facts;
using CouponService.Infrastructure.InMemory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace CouponService.ApiTests.Seeding;

/// <summary>
/// AC-9.5: a completed deployment is a seeded deployment. AC-9.6: re-running converges without
/// manual cleanup.
/// </summary>
public sealed class PolicySeederTests
{
    private static PolicySeeder CreateSeeder(IPolicyRepository policies) =>
        new(policies, StandardFactVocabulary.Create(), NullLogger<PolicySeeder>.Instance);

    [Fact]
    public async Task Seeds_every_deterministic_policy_into_a_cold_store()
    {
        var policies = new InMemoryPolicyRepository();
        var report = await CreateSeeder(policies).SeedAsync(CancellationToken.None);

        Assert.Equal(PolicySeeder.ReadSeedDocuments().Count, report.Total);
        Assert.Equal(report.Total, report.Created);
        Assert.Equal(0, report.Updated);
        Assert.Equal(0, report.Unchanged);

        var stored = await policies.ListAsync();
        Assert.Equal(report.Total, stored.Count);
    }

    [Fact]
    public async Task Re_running_converges_without_duplicating_or_rewriting()
    {
        var policies = new InMemoryPolicyRepository();
        var seeder = CreateSeeder(policies);

        var first = await seeder.SeedAsync(CancellationToken.None);
        var second = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(second.Total, second.Unchanged);
        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Updated);

        var stored = await policies.ListAsync();
        Assert.Equal(first.Total, stored.Count);
    }

    [Fact]
    public async Task A_drifted_document_is_rewritten_rather_than_duplicated()
    {
        var policies = new InMemoryPolicyRepository();
        var seeder = CreateSeeder(policies);
        await seeder.SeedAsync(CancellationToken.None);

        var existing = await policies.GetByPolicyIdAsync("seed-flat5");
        var drifted = existing!.DocumentJson.Replace("\"amount\":5.00", "\"amount\":1.00", StringComparison.Ordinal);
        Assert.NotEqual(existing.DocumentJson, drifted);
        await policies.ReplaceAsync(existing with { DocumentJson = drifted }, existing.ETag);

        var report = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(1, report.Updated);
        Assert.Equal(report.Total - 1, report.Unchanged);
        var restored = await policies.GetByPolicyIdAsync("seed-flat5");
        Assert.Contains("\"amount\":5.00", restored!.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_automatic_policy_is_seeded_so_it_reaches_the_automatic_index()
    {
        var policies = new InMemoryPolicyRepository();
        await CreateSeeder(policies).SeedAsync(CancellationToken.None);

        var automatic = await policies.ListAutomaticAsync();

        Assert.Contains(automatic, record => record.PolicyId == "seed-tuesday10");
    }

    [Fact]
    public async Task Readiness_reports_unhealthy_until_the_seed_has_run()
    {
        var state = new PolicySeedState();
        var check = new PolicySeedHealthCheck(state);
        var context = new HealthCheckContext();

        var pending = await check.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, pending.Status);

        state.MarkSucceeded(new PolicySeedReport(8, 0, 0, 8));
        var seeded = await check.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Healthy, seeded.Status);
    }

    [Fact]
    public async Task A_failed_seed_is_reported_as_unhealthy_with_its_reason()
    {
        var state = new PolicySeedState();
        state.MarkFailed("condition references an unknown fact");

        var result = await new PolicySeedHealthCheck(state).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("unknown fact", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seeding_disabled_leaves_the_store_untouched_and_readiness_healthy()
    {
        var state = new PolicySeedState();
        state.MarkDisabled();

        var result = await new PolicySeedHealthCheck(state).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
