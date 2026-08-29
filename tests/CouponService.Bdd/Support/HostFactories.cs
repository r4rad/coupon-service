extern alias OrderApiHost;

using CouponService.Api.Authentication;
using CouponService.Api.Health;
using CouponService.Api.Options;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using HttpCouponServiceClient = OrderApiHost::OrderApi.Clients.HttpCouponServiceClient;
using ICouponServiceClient = OrderApiHost::OrderApi.Clients.ICouponServiceClient;
using InMemoryOrderRepository = OrderApiHost::OrderApi.Orders.InMemoryOrderRepository;
using IOrderRepository = OrderApiHost::OrderApi.Orders.IOrderRepository;

namespace CouponService.Bdd.Support;

internal sealed class CouponServiceFactory(TokenProvider tokens, MutableClock clock)
    : WebApplicationFactory<Program>
{
    internal InMemoryPolicyRepository Policies { get; } = new();

    internal InMemoryRedemptionRepository Redemptions { get; } = new();

    internal MutableClock Clock { get; } = clock;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in tokens.AuthenticationConfiguration)
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
            services.AddSingleton<IAutomaticPolicyIndex, AutomaticPolicyIndex>();
            services.AddSingleton<IPolicyCandidateResolver, PolicyCandidateResolver>();
            services.AddSingleton<IAutomaticPolicyPreviewService, AutomaticPolicyPreviewService>();

            services.AddHealthChecks()
                .AddCheck<PolicyRepositoryHealthCheck>("policies", tags: ["ready"]);
        });
    }
}

internal sealed class OrderApiFactory(
    CouponServiceFactory couponFactory,
    string couponServiceToken) : WebApplicationFactory<OrderApiHost::Program>
{
    internal InMemoryOrderRepository Orders { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var contentRoot = Path.GetDirectoryName(typeof(OrderApiHost::Program).Assembly.Location)!;
        builder.UseContentRoot(contentRoot);

        _ = couponFactory.Server;
        var baseUrl = couponFactory.Server.BaseAddress!.ToString().TrimEnd('/');

        builder.UseSetting("OrderApi:CouponServiceBaseUrl", baseUrl);
        builder.UseSetting("OrderApi:CouponServiceToken", couponServiceToken);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IOrderRepository>(Orders);

            services.RemoveAll<ICouponServiceClient>();
            services.AddHttpClient<ICouponServiceClient, HttpCouponServiceClient>(client =>
                {
                    client.BaseAddress = couponFactory.Server.BaseAddress;
                    client.Timeout = TimeSpan.FromSeconds(3);
                })
                .ConfigurePrimaryHttpMessageHandler(_ => couponFactory.Server.CreateHandler());
        });
    }
}
