using CouponService.Infrastructure.Logging;

namespace CouponService.Api.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddCouponObservability(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<HttpContextLogEnricher>();
        services.AddSingleton<IDomainEventLogger, SerilogDomainEventLogger>();
        services.AddTransient<CorrelationHttpMessageHandler>();
        return services;
    }

    public static IHttpClientBuilder AddCorrelationPropagation(this IHttpClientBuilder builder) =>
        builder.AddHttpMessageHandler<CorrelationHttpMessageHandler>();
}
