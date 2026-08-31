namespace CouponService.UnitTests.Architecture;

/// <summary>
/// Pins Azure Pipelines CI/CD authorship for CS-26: eight-stage CD, PR gates,
/// WIF auth (AC-9.3), what-if before apply (AC-9.2), test-before-deploy (AC-9.4),
/// and idempotent seeding (AC-9.5, AC-9.6).
/// </summary>
public sealed class AzurePipelinesTests
{
    private static string RepoRoot => RepositoryRoot.Find();

    private static string ReadPipeline() =>
        File.ReadAllText(Path.Combine(RepoRoot, "azure-pipelines.yml"));

    private static string ReadSeedScript() =>
        File.ReadAllText(Path.Combine(RepoRoot, "scripts", "seed-policies.ps1"));

    private static string ReadPrerequisites() =>
        File.ReadAllText(Path.Combine(RepoRoot, "docs", "pipeline-prerequisites.md"));

    private static string StripYamlComments(string yaml) =>
        string.Join(
            Environment.NewLine,
            yaml.Split(['\r', '\n'], StringSplitOptions.None)
                .Select(line =>
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith('#'))
                    {
                        return string.Empty;
                    }

                    var hash = line.IndexOf(" #", StringComparison.Ordinal);
                    return hash >= 0 ? line[..hash] : line;
                }));

    [Fact]
    public void Deploy_builds_container_images_and_updates_apps_before_readiness()
    {
        // CS-29 / section 18 — Deploy replaces the P-11 placeholder with ACR-built images.
        var yaml = ReadPipeline();
        var deployStart = yaml.IndexOf("- stage: Deploy", StringComparison.Ordinal);
        var deployEnd = yaml.IndexOf("- stage: Seed", StringComparison.Ordinal);
        var block = yaml[deployStart..deployEnd];

        Assert.Contains("docker build", block, StringComparison.Ordinal);
        Assert.Contains("docker push", block, StringComparison.Ordinal);
        Assert.Contains("az acr login", block, StringComparison.Ordinal);
        Assert.Contains("src/CouponService.Api/Dockerfile", block, StringComparison.Ordinal);
        Assert.Contains("src/OrderApi/Dockerfile", block, StringComparison.Ordinal);
        Assert.Contains("az containerapp update", block, StringComparison.Ordinal);
        Assert.Contains("/v1/health/ready", block, StringComparison.Ordinal);
        Assert.Contains("azureSubscription: $(azureServiceConnection)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("az acr build", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Bdd_stage_runs_reqnroll_through_the_apim_gateway_urls()
    {
        // AC-10.1 / CS-29 — post-deploy BDD targets APIM /coupons and /orders, not only in-process hosts.
        var yaml = ReadPipeline();
        var bddStart = yaml.IndexOf("- stage: Bdd", StringComparison.Ordinal);
        var bddEnd = yaml.IndexOf("- stage: Verify", StringComparison.Ordinal);
        var block = yaml[bddStart..bddEnd];

        Assert.Contains("tests/CouponService.Bdd/CouponService.Bdd.csproj", block, StringComparison.Ordinal);
        Assert.Contains("BDD_Bdd__Mode: Http", block, StringComparison.Ordinal);
        Assert.Contains("apimGatewayUrl", block, StringComparison.Ordinal);
        Assert.Contains("/coupons", block, StringComparison.Ordinal);
        Assert.Contains("/orders", block, StringComparison.Ordinal);
        Assert.Contains("BDD_Bdd__CouponServiceBaseUrl", block, StringComparison.Ordinal);
        Assert.Contains("BDD_Bdd__OrderApiBaseUrl", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipeline_defines_the_eight_section_18_stages_in_order()
    {
        // AC-9.1 / section 18 — empty-RG path is expressed as this ordered CD sequence.
        var yaml = ReadPipeline();
        string[] stages =
        [
            "- stage: Build",
            "- stage: Test",
            "- stage: Package",
            "- stage: Provision",
            "- stage: Deploy",
            "- stage: Seed",
            "- stage: Bdd",
            "- stage: Verify",
        ];

        var lastIndex = -1;
        foreach (var stage in stages)
        {
            var index = yaml.IndexOf(stage, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Expected stage declaration '{stage}'.");
            Assert.True(index > lastIndex, $"Stage '{stage}' must appear after the previous stage.");
            lastIndex = index;
        }
    }

    [Fact]
    public void Pull_requests_run_build_test_and_bicep_but_never_provision_or_deploy()
    {
        // AC-9.4 / P-14 — CI on PRs must not deploy; CD stages are gated on isCD.
        var yaml = ReadPipeline();
        Assert.Contains("pr:", yaml, StringComparison.Ordinal);
        Assert.Contains("az bicep build", yaml, StringComparison.Ordinal);
        Assert.Contains("az bicep lint", yaml, StringComparison.Ordinal);

        Assert.Contains("name: isCD", yaml, StringComparison.Ordinal);
        Assert.Contains("refs/heads/develop", yaml, StringComparison.Ordinal);
        Assert.Contains("refs/heads/main", yaml, StringComparison.Ordinal);
        Assert.Contains("eq(variables['Build.Reason'], 'Manual')", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("${{ elseif", yaml, StringComparison.Ordinal);
        Assert.Contains("$[iif(", yaml, StringComparison.Ordinal);

        foreach (var stage in new[] { "Package", "Provision", "Deploy", "Seed", "Bdd", "Verify" })
        {
            var marker = $"- stage: {stage}";
            var start = yaml.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing stage {stage}");
            var next = yaml.IndexOf("- stage:", start + marker.Length, StringComparison.Ordinal);
            var block = next >= 0 ? yaml[start..next] : yaml[start..];
            Assert.Contains("eq(variables['isCD'], 'true')", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Pipeline_maps_develop_and_main_to_separate_resource_groups_and_param_files()
    {
        // P-14 — one YAML; branch selects non-prod vs prod RG and bicepparam.
        var yaml = ReadPipeline();
        Assert.Contains("- develop", yaml, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-demo", yaml, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-prod", yaml, StringComparison.Ordinal);
        Assert.Contains("main.dev.bicepparam", yaml, StringComparison.Ordinal);
        Assert.Contains("main.prod.bicepparam", yaml, StringComparison.Ordinal);
        Assert.Contains("bicepParametersFile", yaml, StringComparison.Ordinal);
        Assert.Contains("--parameters \"$(bicepParametersFile)\"", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Test_failure_blocks_provision_and_deploy()
    {
        // AC-9.4 — Test is an ancestor of every deploy-side stage via dependsOn + succeeded().
        var yaml = ReadPipeline();

        Assert.Contains("- stage: Test", yaml, StringComparison.Ordinal);
        Assert.Contains("dependsOn: Build", yaml, StringComparison.Ordinal);
        Assert.Contains("dependsOn: Test", yaml, StringComparison.Ordinal);

        var packageStart = yaml.IndexOf("- stage: Package", StringComparison.Ordinal);
        var packageBlock = yaml[packageStart..yaml.IndexOf("- stage: Provision", StringComparison.Ordinal)];
        Assert.Contains("dependsOn: Test", packageBlock, StringComparison.Ordinal);
        Assert.Contains("succeeded()", packageBlock, StringComparison.Ordinal);

        var provisionStart = yaml.IndexOf("- stage: Provision", StringComparison.Ordinal);
        var provisionBlock = yaml[provisionStart..yaml.IndexOf("- stage: Deploy", StringComparison.Ordinal)];
        Assert.Contains("- Test", provisionBlock, StringComparison.Ordinal);
        Assert.Contains("succeeded()", provisionBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipeline_authenticates_with_workload_identity_federation_and_stores_no_client_secret()
    {
        // AC-9.3 — federated ARM service connection only; no long-lived secret in YAML.
        var yaml = ReadPipeline();
        var code = StripYamlComments(yaml);

        Assert.Contains("azureSubscription: $(azureServiceConnection)", code, StringComparison.Ordinal);
        Assert.Contains("workload-identity", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("federat", yaml, StringComparison.OrdinalIgnoreCase);

        string[] forbidden =
        [
            "clientSecret",
            "client_secret",
            "CLIENT_SECRET",
            "servicePrincipalKey",
            "password:",
            "AccountKey",
            "SharedAccessKey",
        ];

        foreach (var marker in forbidden)
        {
            Assert.DoesNotContain(marker, code, StringComparison.OrdinalIgnoreCase);
        }

        // RG name may appear as a parameter default; deploy commands must use the variable, not a literal.
        Assert.Contains("resourceGroupName", code, StringComparison.Ordinal);
        Assert.Contains("--resource-group \"$(resourceGroupName)\"", code, StringComparison.Ordinal);
        Assert.DoesNotContain("--resource-group rg-coupon-demo", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--resource-group 'rg-coupon-demo'", code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Provision_publishes_what_if_as_an_artifact_before_create()
    {
        // AC-9.2 — what-if result is a pipeline artifact published before apply.
        var yaml = ReadPipeline();
        var provisionStart = yaml.IndexOf("- stage: Provision", StringComparison.Ordinal);
        var provisionEnd = yaml.IndexOf("- stage: Deploy", StringComparison.Ordinal);
        var block = yaml[provisionStart..provisionEnd];

        var whatIf = block.IndexOf("az deployment group what-if", StringComparison.Ordinal);
        var publish = block.IndexOf("PublishPipelineArtifact@1", StringComparison.Ordinal);
        var create = block.IndexOf("az deployment group create", StringComparison.Ordinal);

        Assert.True(whatIf >= 0, "Provision must run az deployment group what-if.");
        Assert.True(publish >= 0, "Provision must publish a pipeline artifact.");
        Assert.True(create >= 0, "Provision must run az deployment group create.");
        Assert.True(whatIf < publish, "what-if must run before the artifact publish.");
        Assert.True(publish < create, "what-if artifact must be published before create (AC-9.2).");
        Assert.Contains("bicep-what-if", block, StringComparison.Ordinal);
        Assert.Contains("$(bicepParametersFile)", block, StringComparison.Ordinal);
        Assert.Contains("infra/bicep/main.bicep", block, StringComparison.Ordinal);
        Assert.DoesNotContain("main.demo.bicepparam", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_stage_invokes_the_idempotent_seed_script()
    {
        // AC-9.5 — deployment completion seeds via scripts/seed-policies.ps1.
        var yaml = ReadPipeline();
        var seedStart = yaml.IndexOf("- stage: Seed", StringComparison.Ordinal);
        var seedEnd = yaml.IndexOf("- stage: Bdd", StringComparison.Ordinal);
        var block = yaml[seedStart..seedEnd];

        Assert.Contains("scripts/seed-policies.ps1", block, StringComparison.Ordinal);
        Assert.Contains("AdminApiBearerToken", block, StringComparison.Ordinal);
        Assert.Contains("get-access-token", block, StringComparison.Ordinal);
        Assert.Contains("couponApiAudience", block, StringComparison.Ordinal);
        Assert.Contains("AzureCLI@2", block, StringComparison.Ordinal);
        Assert.Contains("provision-outputs/outputs.json", block, StringComparison.Ordinal);
        Assert.Contains("couponBackendUrl", block, StringComparison.Ordinal);
        Assert.Contains("UriKind]::Absolute", block, StringComparison.Ordinal);
        Assert.DoesNotContain("apim.TrimEnd('/') + '/coupons'", block, StringComparison.Ordinal);
        Assert.DoesNotContain("SEED_BEARER_TOKEN", block, StringComparison.Ordinal);
        Assert.DoesNotContain("arguments:", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Seed_script_upserts_the_deterministic_policy_set_idempotently()
    {
        // AC-9.5 / AC-9.6 — admin API upsert; re-run converges without manual cleanup.
        Assert.True(File.Exists(Path.Combine(RepoRoot, "scripts", "seed-policies.ps1")));
        var script = ReadSeedScript();

        string[] codes =
        [
            "SAVE10",
            "FLAT5",
            "VEGGIE15",
            "BOGO",
            "EITHER",
            "OLDCODE",
            "LIMITED1",
        ];

        foreach (var code in codes)
        {
            Assert.Contains(code, script, StringComparison.Ordinal);
        }

        Assert.Contains("automatic", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tuesday", script, StringComparison.Ordinal);
        Assert.Contains("/v1/admin/policies", script, StringComparison.Ordinal);
        Assert.Contains("If-Match", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-RestMethod", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Post", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Put", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Get", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("404", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_script_treats_http_404_as_missing_without_throwing()
    {
        // pwsh 7: SkipHttpErrorCheck + StatusCodeVariable — Exception.Response is unreliable.
        var script = ReadSeedScript();
        Assert.Contains("SkipHttpErrorCheck", script, StringComparison.Ordinal);
        Assert.Contains("StatusCodeVariable", script, StringComparison.Ordinal);
        Assert.Contains("AllowedStatusCodes", script, StringComparison.Ordinal);
        Assert.Contains("404", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Prerequisites_document_lists_exactly_the_three_one_time_manual_steps()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot, "docs", "pipeline-prerequisites.md")));
        var docs = ReadPrerequisites();

        Assert.Contains("Azure DevOps project", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workload identity", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("service connection", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Entra", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nothing else is manual", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No GitHub Actions", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rg-coupon-demo", docs, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-prod", docs, StringComparison.Ordinal);
        Assert.Contains("develop", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("User Access Administrator", docs, StringComparison.Ordinal);
        Assert.Contains("eastus2", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_has_no_github_actions_workflows()
    {
        // P-13 — Azure Pipelines is the only CI/CD system.
        var workflows = Path.Combine(RepoRoot, ".github", "workflows");
        if (!Directory.Exists(workflows))
        {
            return;
        }

        var yamlFiles = Directory.GetFiles(workflows, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(workflows, "*.yaml", SearchOption.AllDirectories))
            .ToArray();

        Assert.True(
            yamlFiles.Length == 0,
            "GitHub Actions workflows are forbidden; found: " + string.Join(", ", yamlFiles));
    }
}
