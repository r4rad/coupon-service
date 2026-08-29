namespace CouponService.UnitTests.Architecture;

/// <summary>
/// Pins the live first-apply path for CS-27: eligible demo region parameters,
/// what-if-then-create documentation (AC-9.2), and empty-RG provision narrative (AC-9.1).
/// </summary>
public sealed class LiveProvisionTests
{
    private static string RepoRoot => RepositoryRoot.Find();

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    [Fact]
    public void Demo_parameters_target_an_eligible_region_with_swa_on_a_free_sku_region()
    {
        // Live apply must not hard-require westeurope when the subscription rejects it.
        var parameters = Read(Path.Combine("infra", "bicep", "main.demo.bicepparam"));
        Assert.Contains("param location = 'eastus2'", parameters, StringComparison.Ordinal);
        Assert.Contains("param staticWebAppLocation = 'eastus2'", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("param location = 'westeurope'", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("param location = 'northeurope'", parameters, StringComparison.Ordinal);
        Assert.DoesNotContain("param location = 'eastasia'", parameters, StringComparison.Ordinal);

        // Preferred template default stays westeurope for subscriptions that can use it.
        var main = Read(Path.Combine("infra", "bicep", "main.bicep"));
        Assert.Contains("param location string = 'westeurope'", main, StringComparison.Ordinal);
        Assert.Contains("param staticWebAppLocation string", main, StringComparison.Ordinal);
        Assert.Contains("${uniqueString(resourceGroup().id)}cs28", main, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployment_docs_require_what_if_before_create_and_list_teardown()
    {
        // AC-9.2 — documented apply path publishes what-if before create.
        Assert.True(File.Exists(Path.Combine(RepoRoot, "docs", "deployment.md")));
        var docs = Read(Path.Combine("docs", "deployment.md"));

        Assert.Contains("az deployment group what-if", docs, StringComparison.Ordinal);
        Assert.Contains("az deployment group create", docs, StringComparison.Ordinal);
        Assert.Contains("az group delete --name rg-coupon-demo", docs, StringComparison.Ordinal);

        var whatIf = docs.IndexOf("az deployment group what-if", StringComparison.Ordinal);
        var create = docs.IndexOf("az deployment group create", StringComparison.Ordinal);
        Assert.True(whatIf >= 0 && create > whatIf, "what-if must be documented before create.");
    }

    [Fact]
    public void Deployment_docs_describe_empty_rg_provision_with_nfr6_skus_and_placeholder_image()
    {
        // AC-9.1 / NFR-6 / P-11 — no portal steps; demo SKUs and placeholder called out.
        var docs = Read(Path.Combine("docs", "deployment.md"));

        Assert.Contains("No portal configuration", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Consumption", docs, StringComparison.Ordinal);
        Assert.Contains("Basic", docs, StringComparison.Ordinal);
        Assert.Contains("not Developer", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EnableServerless", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mcr.microsoft.com/k8se/quickstart:latest", docs, StringComparison.Ordinal);
        Assert.Contains("rg-coupon-demo", docs, StringComparison.Ordinal);
    }
}
