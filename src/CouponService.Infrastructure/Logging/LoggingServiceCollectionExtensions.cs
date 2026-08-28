using CouponService.Application.Preview;
using CouponService.Application.Redemption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CouponService.Infrastructure.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddCouponObservabilityLogging(this IServiceCollection services)
    {
        services.TryAddSingleton<IDomainEventLogger, SerilogDomainEventLogger>();
        WrapService<ICouponPreviewService, ObservabilityCouponPreviewService>(services);
        WrapService<ICouponRedeemer, ObservabilityCouponRedeemer>(services);
        return services;
    }

    private static void WrapService<TService, TDecorator>(IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var index = services.ToList().FindIndex(descriptor => descriptor.ServiceType == typeof(TService));
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Service {typeof(TService).Name} must be registered before observability logging.");
        }

        var existing = services[index];
        services.RemoveAt(index);
        services.Insert(
            index,
            ServiceDescriptor.Describe(
                typeof(TService),
                provider =>
                {
                    var inner = ResolveInner(existing, provider);
                    return ActivatorUtilities.CreateInstance<TDecorator>(provider, inner);
                },
                existing.Lifetime));
    }

    private static object ResolveInner(ServiceDescriptor descriptor, IServiceProvider provider)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(provider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Cannot resolve an inner implementation for {descriptor.ServiceType?.Name}.");
    }
}
