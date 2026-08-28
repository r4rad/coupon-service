using CouponService.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace CouponService.Api.Observability;

public sealed class HttpContextLogEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        AddProperty(logEvent, propertyFactory, ObservabilityLogProperties.CorrelationId, context.Items[CorrelationContext.CorrelationIdItemKey]);
        AddProperty(logEvent, propertyFactory, ObservabilityLogProperties.UserId, context.User?.FindFirst("sub")?.Value ?? context.User?.Identity?.Name);
    }

    private static void AddProperty(
        LogEvent logEvent,
        ILogEventPropertyFactory propertyFactory,
        string name,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(name, value));
    }
}

public static class SerilogHostExtensions
{
    public static WebApplicationBuilder AddCouponSerilog(
        this WebApplicationBuilder builder,
        ITextFormatter? formatterOverride)
    {
        builder.Host.UseSerilog(
            (context, services, configuration) =>
            {
                var formatter = formatterOverride ?? new RedactingCompactJsonFormatter();
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.With(services.GetRequiredService<HttpContextLogEnricher>())
                    .WriteTo.Console(formatter);

                if (services.GetService<CollectingLogSink>() is { } collectingSink)
                {
                    configuration.WriteTo.Sink(collectingSink);
                }
            },
            preserveStaticLogger: true);

        return builder;
    }

    public static WebApplicationBuilder AddCouponSerilog(this WebApplicationBuilder builder) =>
        builder.AddCouponSerilog(formatterOverride: null);
}
