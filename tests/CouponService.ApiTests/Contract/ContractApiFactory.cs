using CouponService.Api.Authentication;
using CouponService.Api.Health;
using CouponService.Api.Options;
using CouponService.ApiTests.Auth;
using CouponService.Application.Engine;
using CouponService.Application.Policies;
using CouponService.Application.Preview;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using CouponService.Domain;
using CouponService.Engine.Caching;
using CouponService.Engine.Facts;
using CouponService.Infrastructure.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CouponService.ApiTests.Contract;

public sealed class ContractApiFactory : WebApplicationFactory<Program>
{
    internal InMemoryPolicyRepository Policies { get; } = new();

    internal InMemoryRedemptionRepository Redemptions { get; } = new();

    internal FixedClock Clock { get; } = new(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in TestTokenFactory.TestAuthenticationConfiguration)
        {
            builder.UseSetting(key!, value);
        }

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IOptions<CouponServiceOptions>>(
                Options.Create(new CouponServiceOptions
                {
                    Currency = "EUR",
                    LocalTimeZoneId = "UTC",
                }));

            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<IPolicyRepository>(Policies);
            services.AddSingleton<IRedemptionRepository>(Redemptions);
            services.AddSingleton<IFactRegistry>(_ => StandardFactVocabulary.Create());
            services.AddSingleton<CompiledPolicyCache>();
            services.AddSingleton<IPolicyEngine, PolicyEngine>();
            services.AddSingleton<ICouponValidator, CouponValidator>();
            services.AddSingleton<IPriceCalculator, PriceCalculator>();
            services.AddSingleton<ICouponPreviewService, CouponPreviewService>();
            services.AddSingleton<ICouponRedeemer, CouponRedeemer>();

            services.AddHealthChecks()
                .AddCheck<PolicyRepositoryHealthCheck>("policies", tags: ["ready"]);
        });
    }

    internal HttpClient CreateAnonymousClient() => CreateClient();

    internal HttpClient CreateRedeemClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateToken(AuthorizationPolicies.Redeem));
        return client;
    }

    internal HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateToken(AuthorizationPolicies.Admin));
        return client;
    }
}
