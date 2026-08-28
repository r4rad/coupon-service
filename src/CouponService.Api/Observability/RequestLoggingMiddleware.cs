using System.Diagnostics;
using CouponService.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace CouponService.Api.Observability;

// AC-8.1: structured request logs with correlation id, outcome and duration.
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    IDomainEventLogger domainEvents)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items[CorrelationContext.CorrelationIdItemKey]?.ToString()
            ?? context.TraceIdentifier;
        var userId = context.User?.FindFirst("sub")?.Value
            ?? context.User?.Identity?.Name;

        using (LogContext.PushProperty(ObservabilityLogProperties.CorrelationId, correlationId))
        using (LogContext.PushProperty(ObservabilityLogProperties.UserId, userId))
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            finally
            {
                stopwatch.Stop();
                var outcome = ResolveOutcome(context.Response.StatusCode);
                var durationMs = stopwatch.Elapsed.TotalMilliseconds;

                LogRequestMetadata(context, correlationId, userId, outcome, durationMs);

                if (context.Response.StatusCode >= StatusCodes.Status503ServiceUnavailable)
                {
                    domainEvents.Log(
                        DomainEventNames.CouponServiceUnavailable,
                        new DomainEventContext(UserId: userId));
                }
            }
        }
    }

    private void LogRequestMetadata(
        HttpContext context,
        string correlationId,
        string? userId,
        string outcome,
        double durationMs)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var customerEmail = context.Request.Headers["X-Customer-Email"].ToString();
        var connectionString = context.Request.Headers["X-Connection-String"].ToString();

        logger.LogInformation(
            "Handled {Method} {Path} with correlation {CorrelationId} user {UserId} outcome {Outcome} duration {DurationMs} authorization {AuthorizationHeader} email {CustomerEmail} connection {ConnectionString}",
            context.Request.Method,
            context.Request.Path.Value,
            correlationId,
            userId,
            outcome,
            durationMs,
            authorization,
            customerEmail,
            connectionString);
    }

    private static string ResolveOutcome(int statusCode) =>
        statusCode switch
        {
            >= StatusCodes.Status500InternalServerError => "Error",
            >= StatusCodes.Status400BadRequest => "Failed",
            _ => "Success",
        };
}
