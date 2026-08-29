namespace CouponService.UnitTests.Architecture;

/// <summary>
/// Pins APIM JWT + rate limiting (AC-9.7, AC-7.6) and the managed-identity hop (AC-7.7) for CS-28.
/// </summary>
public sealed class EntraApimManagedIdentityTests
{
    private static string RepoRoot => RepositoryRoot.Find();

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    [Fact]
    public void Apim_customer_and_admin_policies_validate_jwt_and_customer_apis_rate_limit()
    {
        // AC-9.7 / AC-7.6 — gateway JWT and rate limiting authored as policy XML.
        var customer = Read(Path.Combine("infra", "bicep", "policies", "customer-product.xml"));
        Assert.Contains("validate-jwt", customer, StringComparison.Ordinal);
        Assert.Contains("openid-config", customer, StringComparison.Ordinal);
        Assert.Contains("{{jwt-audience}}", customer, StringComparison.Ordinal);
        Assert.Contains("{{entra-tenant-id}}", customer, StringComparison.Ordinal);

        var admin = Read(Path.Combine("infra", "bicep", "policies", "admin-product.xml"));
        Assert.Contains("validate-jwt", admin, StringComparison.Ordinal);
        Assert.Contains("Coupon.Admin", admin, StringComparison.Ordinal);
        Assert.Contains("required-claims", admin, StringComparison.Ordinal);

        var rateLimit = Read(Path.Combine("infra", "bicep", "policies", "customer-api-rate-limit.xml"));
        Assert.Contains("rate-limit", rateLimit, StringComparison.Ordinal);
        Assert.Contains("calls=\"60\"", rateLimit, StringComparison.Ordinal);

        var apimApi = Read(Path.Combine("infra", "bicep", "modules", "apim-api.bicep"));
        Assert.Contains("loadTextContent('../policies/customer-product.xml')", apimApi, StringComparison.Ordinal);
        Assert.Contains("loadTextContent('../policies/admin-product.xml')", apimApi, StringComparison.Ordinal);
        Assert.Contains("loadTextContent('../policies/customer-api-rate-limit.xml')", apimApi, StringComparison.Ordinal);
        Assert.Contains("urlTemplate: '/v1/coupons/preview'", apimApi, StringComparison.Ordinal);
        Assert.DoesNotContain("/v1/reservations", apimApi, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_jwt_bearer_revalidates_tokens_with_entra_role_claim_mapping()
    {
        // AC-7.6 — double validation: gateway validate-jwt plus application JwtBearer.
        var auth = Read(Path.Combine(
            "src",
            "CouponService.Api",
            "Authentication",
            "AuthenticationServiceCollectionExtensions.cs"));

        Assert.Contains("AddJwtBearer", auth, StringComparison.Ordinal);
        Assert.Contains("RoleClaimType = \"roles\"", auth, StringComparison.Ordinal);
        Assert.Contains("ValidAudience", auth, StringComparison.Ordinal);
        Assert.Contains("authOptions.Jwt.Authority", auth, StringComparison.Ordinal);
    }

    [Fact]
    public void Order_api_uses_managed_identity_token_provider_without_a_shared_secret()
    {
        // AC-7.7 — MI hop; configuration token is only for local UseManagedIdentity=false.
        var provider = Read(Path.Combine("src", "OrderApi", "Auth", "ICouponServiceTokenProvider.cs"));
        Assert.Contains("class ManagedIdentityCouponServiceTokenProvider", provider, StringComparison.Ordinal);
        Assert.Contains("IDENTITY_ENDPOINT", provider, StringComparison.Ordinal);
        Assert.Contains("AZURE_CLIENT_ID", provider, StringComparison.Ordinal);
        var managedIdentityBody = provider.Split("class ManagedIdentityCouponServiceTokenProvider", 2)[1];
        Assert.DoesNotContain("options.Value.CouponServiceToken", managedIdentityBody, StringComparison.Ordinal);
        Assert.DoesNotContain("CouponServiceToken must be configured", managedIdentityBody, StringComparison.Ordinal);

        var program = Read(Path.Combine("src", "OrderApi", "Program.cs"));
        Assert.Contains("ManagedIdentityCouponServiceTokenProvider", program, StringComparison.Ordinal);
        Assert.Contains("UseManagedIdentity", program, StringComparison.Ordinal);

        var options = Read(Path.Combine("src", "OrderApi", "Options", "OrderApiOptions.cs"));
        Assert.Contains("UseManagedIdentity", options, StringComparison.Ordinal);
        Assert.Contains("CouponServiceResource", options, StringComparison.Ordinal);

        var containerApps = Read(Path.Combine("infra", "bicep", "modules", "containerapps.bicep"));
        Assert.Contains("OrderApi__UseManagedIdentity", containerApps, StringComparison.Ordinal);
        Assert.Contains("value: 'true'", containerApps, StringComparison.Ordinal);
        Assert.Contains("OrderApi__CouponServiceResource", containerApps, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderApi__CouponServiceToken", containerApps, StringComparison.Ordinal);
    }

    [Fact]
    public void Authentication_docs_list_entra_apps_roles_apim_jwt_and_managed_identity_hop()
    {
        Assert.True(File.Exists(Path.Combine(RepoRoot, "docs", "authentication.md")));
        var docs = Read(Path.Combine("docs", "authentication.md"));

        Assert.Contains("Coupon.Redeem", docs, StringComparison.Ordinal);
        Assert.Contains("Coupon.Admin", docs, StringComparison.Ordinal);
        Assert.Contains("validate-jwt", docs, StringComparison.Ordinal);
        Assert.Contains("rate-limit", docs, StringComparison.Ordinal);
        Assert.Contains("managed identity", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api://coupon-service", docs, StringComparison.Ordinal);
        Assert.Contains("{customer-spa-client-id}", docs, StringComparison.Ordinal);
        Assert.Contains("never", docs, StringComparison.OrdinalIgnoreCase);
    }
}
