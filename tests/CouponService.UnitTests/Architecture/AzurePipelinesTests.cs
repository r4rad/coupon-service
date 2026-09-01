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

    private static string ReadEntraSetupScript() =>
        File.ReadAllText(Path.Combine(RepoRoot, "scripts", "setup-entra-app.ps1"));

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
    public void Smoke_stage_verifies_the_deployment_through_the_apim_gateway_urls()
    {
        // The deployed stack has no controllable clock and no admin write path, so this stage
        // smokes the deployment through APIM /coupons and /orders. The Reqnroll scenarios that
        // need those affordances run in process in the Test stage (AC-10.1).
        var yaml = ReadPipeline();
        var smokeStart = yaml.IndexOf("- stage: Smoke", StringComparison.Ordinal);
        var smokeEnd = yaml.IndexOf("- stage: Verify", StringComparison.Ordinal);
        var block = yaml[smokeStart..smokeEnd];

        Assert.Contains("scripts/smoke-deployed-stack.ps1", block, StringComparison.Ordinal);
        Assert.Contains("apimGatewayUrl", block, StringComparison.Ordinal);
        Assert.Contains("/coupons", block, StringComparison.Ordinal);
        Assert.Contains("/orders", block, StringComparison.Ordinal);

        // Only the service connection can mint the Entra token the authenticated check needs.
        Assert.Contains("azureSubscription: $(azureServiceConnection)", block, StringComparison.Ordinal);
        Assert.Contains("couponApiAudience", block, StringComparison.Ordinal);

        // A test token cannot survive APIM validate-jwt, and the deployed app disables the scheme.
        Assert.DoesNotContain("TokenStrategy", block, StringComparison.Ordinal);
        Assert.DoesNotContain("BDD_Bdd__Mode", block, StringComparison.Ordinal);
        Assert.DoesNotContain("CouponService.Bdd.csproj", block, StringComparison.Ordinal);
        Assert.DoesNotContain("arguments:", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_smoke_script_asserts_money_and_not_only_status_codes()
    {
        // A routing check that never looks at the discount would pass against a stack whose
        // seeded policies had silently failed to load.
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "smoke-deployed-stack.ps1"));

        Assert.Contains("/v1/health/live", script, StringComparison.Ordinal);
        Assert.Contains("/v1/health/ready", script, StringComparison.Ordinal);
        Assert.Contains("/v1/coupons/preview", script, StringComparison.Ordinal);
        Assert.Contains("SAVE10", script, StringComparison.Ordinal);
        Assert.Contains("get-access-token", script, StringComparison.Ordinal);

        // The unauthenticated calls must be asserted as rejected, not merely attempted.
        Assert.Contains("-Expected 401", script, StringComparison.Ordinal);
        Assert.Contains("-Expected 200", script, StringComparison.Ordinal);

        // Discount on a 40.00 basket under a 10 percent seeded policy.
        Assert.Contains("[decimal] 4.00", script, StringComparison.Ordinal);
        Assert.Contains("[decimal] 36.00", script, StringComparison.Ordinal);
        Assert.Contains("'Applied'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_stage_resolves_the_backend_url_without_trusting_an_unset_macro()
    {
        // When AdminApiBaseUrl is unset, ADO expands $(AdminApiBaseUrl) to the literal string.
        // A bash `[ -z ]` check treats that as present, so curl gets a bad hostname. The stage
        // must discard the literal and fall back to couponBackendUrl from provision outputs.
        var yaml = ReadPipeline();
        var verifyStart = yaml.IndexOf("- stage: Verify", StringComparison.Ordinal);
        Assert.True(verifyStart >= 0, "Verify stage is missing.");
        var block = yaml[verifyStart..];

        Assert.Contains("couponBackendUrl", block, StringComparison.Ordinal);
        Assert.Contains("UriKind]::Absolute", block, StringComparison.Ordinal);
        Assert.Contains("/v1/health/live", block, StringComparison.Ordinal);
        Assert.Contains("/v1/health/ready", block, StringComparison.Ordinal);
        Assert.Contains(@"^\$\([A-Za-z0-9_]+\)$", block, StringComparison.Ordinal);

        Assert.DoesNotContain("curl -fsS", block, StringComparison.Ordinal);
        Assert.DoesNotContain("[ -z \"$COUPON_URL\" ]", block, StringComparison.Ordinal);
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
            "- stage: Smoke",
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

        foreach (var stage in new[] { "Package", "Provision", "Deploy", "Seed", "Smoke", "Verify" })
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
    public void Seed_stage_verifies_the_startup_seed_without_holding_a_credential()
    {
        // AC-9.5 — the service seeds itself as it starts, so CD verifies through the anonymous
        // readiness probe. No admin token means no Entra dependency in the deployment path.
        var yaml = ReadPipeline();
        var seedStart = yaml.IndexOf("- stage: Seed", StringComparison.Ordinal);
        var seedEnd = yaml.IndexOf("- stage: Smoke", StringComparison.Ordinal);
        var block = yaml[seedStart..seedEnd];

        Assert.Contains("/v1/health/ready", block, StringComparison.Ordinal);
        Assert.Contains("provision-outputs/outputs.json", block, StringComparison.Ordinal);
        Assert.Contains("couponBackendUrl", block, StringComparison.Ordinal);
        Assert.Contains("UriKind]::Absolute", block, StringComparison.Ordinal);
        Assert.Contains("src/CouponService.Api/Seeding/SeedPolicies.json", block, StringComparison.Ordinal);

        Assert.DoesNotContain("get-access-token", block, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminApiBearerToken", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", block, StringComparison.Ordinal);
        Assert.DoesNotContain("arguments:", block, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Entra_setup_script_requests_v2_tokens_and_an_application_assignable_admin_role()
    {
        // JwtBearer pins ValidIssuer to the v2 authority (main.bicep sets jwtIssuer =
        // jwtAuthority), so a version 1 registration issues iss = sts.windows.net and fails
        // validation. A Users-only Coupon.Admin cannot be assigned to the pipeline principal.
        Assert.True(File.Exists(Path.Combine(RepoRoot, "scripts", "setup-entra-app.ps1")));
        var script = ReadEntraSetupScript();

        Assert.Contains("requestedAccessTokenVersion = 2", script, StringComparison.Ordinal);
        Assert.Contains("'Application', 'User'", script, StringComparison.Ordinal);
        Assert.Contains("Coupon.Admin", script, StringComparison.Ordinal);
        Assert.Contains("Coupon.Redeem", script, StringComparison.Ordinal);
        Assert.Contains("appRoleAssignedTo", script, StringComparison.Ordinal);
        Assert.Contains("api://coupon-service", script, StringComparison.Ordinal);
        Assert.DoesNotContain("clientSecret", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Seed_script_upserts_the_deterministic_policy_set_idempotently()
    {
        // AC-9.5 / AC-9.6 — admin API upsert; re-run converges without manual cleanup. The script
        // remains the manual path; CD relies on the application's startup seeder.
        Assert.True(File.Exists(Path.Combine(RepoRoot, "scripts", "seed-policies.ps1")));
        var script = ReadSeedScript();

        Assert.Contains("/v1/admin/policies", script, StringComparison.Ordinal);
        Assert.Contains("If-Match", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-RestMethod", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Post", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Put", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("404", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_deterministic_policy_set_has_one_definition_shared_by_script_and_application()
    {
        // Two copies of the seed set drift silently, and the drift only shows up as a demo that
        // behaves differently depending on which path seeded it.
        var seedFile = Path.Combine(RepoRoot, "src", "CouponService.Api", "Seeding", "SeedPolicies.json");
        Assert.True(File.Exists(seedFile));
        var seed = File.ReadAllText(seedFile);

        string[] codes = ["SAVE10", "FLAT5", "VEGGIE15", "BOGO", "EITHER", "OLDCODE", "LIMITED1"];
        foreach (var code in codes)
        {
            Assert.Contains(code, seed, StringComparison.Ordinal);
        }

        Assert.Contains("automatic", seed, StringComparison.Ordinal);
        Assert.Contains("Tuesday", seed, StringComparison.Ordinal);

        var script = ReadSeedScript();
        Assert.Contains("src/CouponService.Api/Seeding/SeedPolicies.json", script, StringComparison.Ordinal);
        foreach (var code in codes)
        {
            Assert.DoesNotContain(code, script, StringComparison.Ordinal);
        }

        var csproj = File.ReadAllText(Path.Combine(RepoRoot, "src", "CouponService.Api", "CouponService.Api.csproj"));
        Assert.Contains("Seeding\\SeedPolicies.json", csproj, StringComparison.Ordinal);
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
