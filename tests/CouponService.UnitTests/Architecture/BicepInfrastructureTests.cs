namespace CouponService.UnitTests.Architecture;

/// <summary>
/// Pins IaC authorship for CS-25: empty-RG provisionability (AC-9.1),
/// what-if-ready parameters with no secrets (AC-9.2), and demo SKUs (NFR-6).
/// </summary>
public sealed class BicepInfrastructureTests
{
    private static string BicepRoot => Path.Combine(RepositoryRoot.Find(), "infra", "bicep");

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(BicepRoot, relativePath));

    [Fact]
    public void Main_bicep_composes_every_module_needed_to_provision_from_an_empty_resource_group()
    {
        // AC-9.1 — pipeline against an empty RG needs the full module set, no portal steps.
        var main = Read("main.bicep");
        Assert.Contains("targetScope = 'resourceGroup'", main, StringComparison.Ordinal);

        string[] requiredModules =
        [
            "modules/observability.bicep",
            "modules/identity.bicep",
            "modules/keyvault.bicep",
            "modules/cosmos.bicep",
            "modules/acr.bicep",
            "modules/containerapps.bicep",
            "modules/appservice.bicep",
            "modules/apim.bicep",
            "modules/apim-api.bicep",
            "modules/staticwebapp.bicep",
        ];

        foreach (var modulePath in requiredModules)
        {
            Assert.True(
                File.Exists(Path.Combine(BicepRoot, modulePath.Replace('/', Path.DirectorySeparatorChar))),
                $"Expected module '{modulePath}' to exist.");
            Assert.Contains($"'{modulePath}'", main, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("rg-coupon-demo", main, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uniqueString(resourceGroup().id)", main, StringComparison.Ordinal);
        Assert.Contains("param location string = 'westeurope'", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_parameters_carry_no_secrets_and_leave_resource_group_to_the_deployment_command()
    {
        // AC-9.2 — what-if/create consume this file; secrets or a baked-in RG would force portal work.
        Assert.True(File.Exists(Path.Combine(BicepRoot, "main.demo.bicepparam")));
        var parameters = Read("main.demo.bicepparam");

        Assert.Contains("using './main.bicep'", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("rg-coupon-demo", parameters, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resourceGroup", parameters, StringComparison.OrdinalIgnoreCase);

        // Strip comments so prose cannot trip secret-marker checks.
        var withoutComments = string.Join(
            Environment.NewLine,
            parameters
                .Split(['\r', '\n'], StringSplitOptions.None)
                .Select(line =>
                {
                    var comment = line.IndexOf("//", StringComparison.Ordinal);
                    return comment >= 0 ? line[..comment] : line;
                }));

        string[] forbiddenSecretMarkers =
        [
            "password",
            "secret",
            "clientSecret",
            "connectionString",
            "AccountKey",
            "SharedAccessKey",
            "PRIMARYKEY",
            "INSTRUMENTATIONKEY",
        ];

        foreach (var marker in forbiddenSecretMarkers)
        {
            Assert.DoesNotContain(marker, withoutComments, StringComparison.OrdinalIgnoreCase);
        }

        var main = Read("main.bicep");
        Assert.Contains("targetScope = 'resourceGroup'", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_skus_stay_within_free_or_near_free_bounds_with_acr_basic_as_the_only_paid_sku()
    {
        // NFR-6 — section 17 SKUs pinned in modules, not left open for a paid default.
        var apim = Read(Path.Combine("modules", "apim.bicep"));
        Assert.Contains("name: 'Consumption'", apim, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'Developer'", apim, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'Standard'", apim, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'Premium'", apim, StringComparison.Ordinal);

        var cosmos = Read(Path.Combine("modules", "cosmos.bicep"));
        Assert.Contains("name: 'EnableServerless'", cosmos, StringComparison.Ordinal);
        Assert.Contains("enableFreeTier: enableFreeTier", cosmos, StringComparison.Ordinal);

        var acr = Read(Path.Combine("modules", "acr.bicep"));
        Assert.Contains("name: 'Basic'", acr, StringComparison.Ordinal);
        // ACR Basic is the only accepted charge — Standard/Premium would violate NFR-6.
        Assert.DoesNotContain("name: 'Standard'", acr, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'Premium'", acr, StringComparison.Ordinal);

        var staticWebApp = Read(Path.Combine("modules", "staticwebapp.bicep"));
        Assert.Contains("name: 'Free'", staticWebApp, StringComparison.Ordinal);
        Assert.Contains("tier: 'Free'", staticWebApp, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'Standard'", staticWebApp, StringComparison.Ordinal);

        var observability = Read(Path.Combine("modules", "observability.bicep"));
        Assert.Contains("dailyQuotaGb: dailyCapGb", observability, StringComparison.Ordinal);

        var appService = Read(Path.Combine("modules", "appservice.bicep"));
        Assert.Contains("name: 'F1'", appService, StringComparison.Ordinal);
        Assert.Contains("tier: 'Free'", appService, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'B1'", appService, StringComparison.Ordinal);
        Assert.DoesNotContain("name: 'S1'", appService, StringComparison.Ordinal);

        var containerApps = Read(Path.Combine("modules", "containerapps.bicep"));
        Assert.Contains("minReplicas: 0", containerApps, StringComparison.Ordinal);
        Assert.DoesNotContain("workloadProfiles", containerApps, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Container_apps_are_provisioned_with_a_public_placeholder_image()
    {
        // P-11 — first deploy into an empty RG must not deadlock on an empty ACR.
        var containerApps = Read(Path.Combine("modules", "containerapps.bicep"));
        Assert.Contains("param placeholderImage string = 'mcr.microsoft.com/", containerApps, StringComparison.Ordinal);
        Assert.Contains("image: placeholderImage", containerApps, StringComparison.Ordinal);

        var main = Read("main.bicep");
        Assert.Contains("param placeholderImage string = 'mcr.microsoft.com/", main, StringComparison.Ordinal);
        Assert.Contains("placeholderImage: placeholderImage", main, StringComparison.Ordinal);

        var parameters = Read("main.demo.bicepparam");
        Assert.Contains("mcr.microsoft.com/", parameters, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_module_requires_project_env_and_owner_tags()
    {
        string[] modules =
        [
            "observability.bicep",
            "identity.bicep",
            "keyvault.bicep",
            "cosmos.bicep",
            "acr.bicep",
            "containerapps.bicep",
            "appservice.bicep",
            "apim.bicep",
            "staticwebapp.bicep",
        ];

        foreach (var module in modules)
        {
            var content = Read(Path.Combine("modules", module));
            Assert.Contains("param tags object", content, StringComparison.Ordinal);
            Assert.Contains("tags: tags", content, StringComparison.Ordinal);
        }

        var main = Read("main.bicep");
        Assert.Contains("project: projectName", main, StringComparison.Ordinal);
        Assert.Contains("env: environmentName", main, StringComparison.Ordinal);
        Assert.Contains("owner: ownerTag", main, StringComparison.Ordinal);
    }
}
