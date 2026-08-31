namespace CouponService.UnitTests.Architecture;

/// <summary>
/// Pins CS-30 delivery docs: deployment and authentication write-ups, assumptions,
/// README discoverability, and the P-12 APIM cache correction.
/// </summary>
public sealed class Cs30DocumentationTests
{
    private static string RepoRoot => RepositoryRoot.Find();

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    [Fact]
    public void Readme_links_to_deployment_authentication_prerequisites_and_assumptions()
    {
        var readme = Read("README.md");

        Assert.Contains("docs/deployment.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/authentication.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/pipeline-prerequisites.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/assumptions.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/solution-architecture.md", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Assumptions_document_currency_region_skus_private_ado_dual_rgs_and_deferred_work()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot, "docs", "assumptions.md")));
        var docs = Read(Path.Combine("docs", "assumptions.md"));

        Assert.Contains("decimal", docs, StringComparison.Ordinal);
        Assert.Contains("eastus2", docs, StringComparison.Ordinal);
        Assert.Contains("Consumption", docs, StringComparison.Ordinal);
        Assert.Contains("private", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rg-coupon-demo", docs, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-prod", docs, StringComparison.Ordinal);
        Assert.Contains("simulate", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shadow", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SPA", docs, StringComparison.Ordinal);
        Assert.Contains("P-12", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployment_docs_link_pipeline_yaml_param_files_and_empty_rg_cd_path()
    {
        // AC-9.1 — reviewer can follow from docs to the pipeline and param files without searching.
        var docs = Read(Path.Combine("docs", "deployment.md"));

        Assert.Contains("azure-pipelines.yml", docs, StringComparison.Ordinal);
        Assert.Contains("main.dev.bicepparam", docs, StringComparison.Ordinal);
        Assert.Contains("main.prod.bicepparam", docs, StringComparison.Ordinal);
        Assert.Contains("feature", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("develop", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("main", docs, StringComparison.Ordinal);
        Assert.Contains("Build", docs, StringComparison.Ordinal);
        Assert.Contains("Provision", docs, StringComparison.Ordinal);
        Assert.Contains("Seed", docs, StringComparison.Ordinal);
        Assert.Contains("BDD", docs, StringComparison.Ordinal);
        Assert.Contains("No portal configuration", docs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authentication_docs_describe_double_validation_apim_jwt_and_managed_identity_hop()
    {
        // AC-9.7, AC-7.6, AC-7.7 — auth write-up matches the implemented edge and hop.
        var docs = Read(Path.Combine("docs", "authentication.md"));

        Assert.Contains("validate-jwt", docs, StringComparison.Ordinal);
        Assert.Contains("rate-limit", docs, StringComparison.Ordinal);
        Assert.Contains("AC-7.6", docs, StringComparison.Ordinal);
        Assert.Contains("AC-7.7", docs, StringComparison.Ordinal);
        Assert.Contains("AC-9.7", docs, StringComparison.Ordinal);
        Assert.Contains("defence in depth", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("managed identity", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("through APIM", docs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authentication_docs_describe_local_test_token_startup_guard()
    {
        // AC-7.5 / P-8 — misconfiguration outside Development or Test must fail fast.
        var docs = Read(Path.Combine("docs", "authentication.md"));
        var guard = Read(Path.Combine(
            "src",
            "CouponService.Api",
            "Authentication",
            "TestTokenStartupGuard.cs"));

        Assert.Contains("TestTokenStartupGuard", docs, StringComparison.Ordinal);
        Assert.Contains("Development", docs, StringComparison.Ordinal);
        Assert.Contains("Test", docs, StringComparison.Ordinal);
        Assert.Contains("throws at startup", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InvalidOperationException", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_architecture_drops_apim_response_cache_and_documents_p12_caching()
    {
        // P-12 — Consumption tier has no internal cache; SWA CDN + backend headers instead.
        var docs = Read(Path.Combine("docs", "solution-architecture.md"));

        Assert.DoesNotContain("APIM response cache", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Static Web Apps CDN", docs, StringComparison.Ordinal);
        Assert.Contains("Cache-Control", docs, StringComparison.Ordinal);
        Assert.Contains("ETag", docs, StringComparison.Ordinal);
        Assert.Contains("P-12", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_architecture_and_prerequisites_document_p13_p14_pipeline_branching()
    {
        var architecture = Read(Path.Combine("docs", "solution-architecture.md"));
        var prerequisites = Read(Path.Combine("docs", "pipeline-prerequisites.md"));

        Assert.Contains("P-13", architecture, StringComparison.Ordinal);
        Assert.Contains("P-14", architecture, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-demo", architecture, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-prod", architecture, StringComparison.Ordinal);
        Assert.Contains("main.dev.bicepparam", architecture, StringComparison.Ordinal);
        Assert.Contains("main.prod.bicepparam", architecture, StringComparison.Ordinal);

        Assert.Contains("P-13", prerequisites, StringComparison.Ordinal);
        Assert.Contains("P-14", prerequisites, StringComparison.Ordinal);
        Assert.Contains("private", prerequisites, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("User Access Administrator", prerequisites, StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_stage_passes_admin_url_and_token_through_environment_variables()
    {
        // Token via env (not PowerShell@2 arguments). URL resolved and seeded in one step
        // so a second-step $(resolvedAdminBaseUrl) env handoff cannot expand empty.
        var yaml = Read(Path.Combine("azure-pipelines.yml"));
        var seedStart = yaml.IndexOf("- stage: Seed", StringComparison.Ordinal);
        var seedEnd = yaml.IndexOf("- stage: Bdd", StringComparison.Ordinal);
        var block = yaml[seedStart..seedEnd];

        Assert.Contains("scripts/seed-policies.ps1", block, StringComparison.Ordinal);
        Assert.Contains("SEED_BEARER_TOKEN", block, StringComparison.Ordinal);
        Assert.Contains("UriKind]::Absolute", block, StringComparison.Ordinal);
        Assert.Contains("apimGatewayUrl", block, StringComparison.Ordinal);
        Assert.DoesNotContain("arguments:", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SEED_BASE_URL", block, StringComparison.Ordinal);
        Assert.DoesNotContain("task.setvariable variable=resolvedAdminBaseUrl", block, StringComparison.Ordinal);
    }
}
