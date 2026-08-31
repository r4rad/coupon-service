using CouponService.Api.Health;
using CouponService.Api.Options;
using CouponService.Api.Seeding;
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
using CouponService.Infrastructure.Logging;
using Microsoft.Extensions.Options;

namespace CouponService.Api.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddCouponService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CouponServiceOptions>()
            .Bind(configuration.GetSection(CouponServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CouponServiceOptions>, CouponServiceOptionsValidator>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPolicyRepository, InMemoryPolicyRepository>();
        services.AddSingleton<IRedemptionRepository, InMemoryRedemptionRepository>();
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

        services.AddOptions<PolicySeedOptions>()
            .Bind(configuration.GetSection(PolicySeedOptions.SectionName));

        services.AddSingleton<PolicySeedState>();
        services.AddSingleton<PolicySeeder>();
        services.AddHostedService<PolicySeedHostedService>();

        services.AddHealthChecks()
            .AddCheck<PolicyRepositoryHealthCheck>("policies", tags: ["ready"])
            .AddCheck<PolicySeedHealthCheck>("policy-seed", tags: ["ready"]);

        services.AddCouponObservabilityLogging();

        return services;
    }
}
