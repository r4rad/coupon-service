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

namespace CouponService.ApiTests.Ordering;

internal sealed class OrderingCouponServiceFactory : WebApplicationFactory<Program>
{
    internal InMemoryPolicyRepository Policies { get; } = new();

    internal InMemoryRedemptionRepository Redemptions { get; } = new();

    internal FixedClock Clock { get; } = new(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in Auth.TestTokenFactory.TestAuthenticationConfiguration)
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

    public new HttpClient CreateClient() =>
        CreateClient(new WebApplicationFactoryClientOptions());

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        var client = base.CreateClient(options);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            Auth.TestTokenFactory.CreateToken(AuthorizationPolicies.Redeem));
        return client;
    }
}

internal sealed class OrderingOrderApiFactory : WebApplicationFactory<OrderApiHost::Program>
{
    private readonly OrderingCouponServiceFactory _couponFactory;
    private readonly string _couponServiceToken;
    private readonly string? _couponServiceBaseUrlOverride;

    internal OrderingOrderApiFactory(
        OrderingCouponServiceFactory couponFactory,
        string couponServiceToken,
        string? couponServiceBaseUrlOverride = null)
    {
        _couponFactory = couponFactory;
        _couponServiceToken = couponServiceToken;
        _couponServiceBaseUrlOverride = couponServiceBaseUrlOverride;
    }

    internal InMemoryOrderRepository Orders { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var contentRoot = Path.GetDirectoryName(typeof(OrderApiHost::Program).Assembly.Location)!;
        builder.UseContentRoot(contentRoot);

        _ = _couponFactory.Server;
        var baseUrl = _couponServiceBaseUrlOverride
            ?? _couponFactory.Server.BaseAddress!.ToString().TrimEnd('/');

        builder.UseSetting("OrderApi:CouponServiceBaseUrl", baseUrl);
        builder.UseSetting("OrderApi:CouponServiceToken", _couponServiceToken);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IOrderRepository>(Orders);

            if (_couponServiceBaseUrlOverride is null)
            {
                services.RemoveAll<ICouponServiceClient>();
                services.AddHttpClient<ICouponServiceClient, HttpCouponServiceClient>(client =>
                    {
                        client.BaseAddress = _couponFactory.Server.BaseAddress;
                        client.Timeout = TimeSpan.FromSeconds(3);
                    })
                    .ConfigurePrimaryHttpMessageHandler(_ => _couponFactory.Server.CreateHandler());
            }
        });
    }
}
