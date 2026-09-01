extern alias OrderApiHost;

using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using IClock = OrderApiHost::OrderApi.Services.IClock;
using ManagedIdentityCouponServiceTokenProvider = OrderApiHost::OrderApi.Auth.ManagedIdentityCouponServiceTokenProvider;
using OrderApiOptions = OrderApiHost::OrderApi.Options.OrderApiOptions;

namespace CouponService.ApiTests.OrderApi;

/// <summary>
/// Behaviour of the Order → Coupon managed-identity token hop (AC-7.7).
/// </summary>
public sealed class ManagedIdentityTokenProviderTests
{
    [Fact]
    public async Task Managed_identity_provider_requests_token_from_identity_endpoint_without_using_configuration_secret()
    {
        var handler = new StubIdentityHandler(
            """{"access_token":"mi-access-token","expires_on":"2000000000"}""");
        using var factory = new SingleHandlerHttpClientFactory(handler);

        Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", "http://127.0.0.1:9/msi/token");
        Environment.SetEnvironmentVariable("IDENTITY_HEADER", "test-identity-header");
        Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", "order-mi-client-id");
        try
        {
            var provider = new ManagedIdentityCouponServiceTokenProvider(
                factory,
                Options.Create(new OrderApiOptions
                {
                    UseManagedIdentity = true,
                    CouponServiceScope = "api://coupon-service/.default",
                    CouponServiceToken = "must-not-be-used",
                }),
                new FixedOrderClock(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)));

            var token = await provider.GetTokenAsync();

            Assert.Equal("mi-access-token", token);
            Assert.NotNull(handler.LastRequest);
            Assert.Equal("test-identity-header", handler.LastRequest!.Headers.GetValues("X-IDENTITY-HEADER").Single());
            Assert.Contains("resource=api%3A%2F%2Fcoupon-service%2F.default", handler.LastRequest.RequestUri!.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("scope=", handler.LastRequest.RequestUri.Query, StringComparison.Ordinal);
            Assert.Contains("client_id=order-mi-client-id", handler.LastRequest.RequestUri.Query, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-be-used", handler.LastRequest.RequestUri.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("IDENTITY_HEADER", null);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", null);
        }
    }

    [Fact]
    public async Task Managed_identity_provider_caches_token_until_near_expiry()
    {
        var handler = new StubIdentityHandler(
            """{"access_token":"cached-token","expires_on":"2000000000"}""");
        using var factory = new SingleHandlerHttpClientFactory(handler);

        Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", "http://127.0.0.1:9/msi/token");
        Environment.SetEnvironmentVariable("IDENTITY_HEADER", "hdr");
        try
        {
            var provider = new ManagedIdentityCouponServiceTokenProvider(
                factory,
                Options.Create(new OrderApiOptions { CouponServiceScope = "api://coupon-service/.default" }),
                new FixedOrderClock(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero)));

            Assert.Equal("cached-token", await provider.GetTokenAsync());
            Assert.Equal("cached-token", await provider.GetTokenAsync());
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", null);
            Environment.SetEnvironmentVariable("IDENTITY_HEADER", null);
        }
    }

    private sealed class FixedOrderClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class StubIdentityHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new(handler);

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }
}
