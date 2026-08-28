using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CouponService.Api.Authentication;
using CouponService.Application.Policies;
using CouponService.ApiTests.Reservations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CouponService.ApiTests.Auth;

public sealed class AuthenticationEndpointTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthenticationEndpointTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reservation_without_token_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve("SAVE10", "order-no-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reservation_with_customer_token_returns_403()
    {
        using var client = CreateClientWithRoles();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve("SAVE10", "order-customer-token"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reservation_with_redeem_role_returns_201()
    {
        const string code = "SAVE10-AUTH";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-auth"));

        using var client = CreateClientWithRoles(AuthorizationPolicies.Redeem);
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-redeem-token"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Admin_without_token_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/admin/policies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_redeem_role_returns_403()
    {
        using var client = CreateClientWithRoles(AuthorizationPolicies.Redeem);
        var response = await client.GetAsync("/v1/admin/policies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_with_admin_role_returns_200()
    {
        using var client = CreateClientWithRoles(AuthorizationPolicies.Admin);
        var response = await client.GetAsync("/v1/admin/policies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateToken(roles));
        return client;
    }

    private async Task SeedPolicyAsync(string documentJson)
    {
        var record = PolicyRecordFactory.FromDocument(documentJson);
        if (await _factory.Policies.GetByPartitionKeyAsync(record.PartitionKey) is not null)
        {
            return;
        }

        _ = await _factory.Policies.CreateAsync(record);
    }
}

public sealed class TestTokenStartupGuardTests
{
    [Fact]
    public void Startup_throws_when_test_token_enabled_in_production()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting($"{AuthenticationOptions.SectionName}:TestToken:Enabled", "true");
            });

            _ = factory.CreateClient();
        });

        Assert.Contains("Test token authentication is enabled", exception.Message, StringComparison.Ordinal);
    }
}
