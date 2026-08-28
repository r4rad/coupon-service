namespace CouponService.Api.Observability;

// AC-8.2: propagate trace context on outgoing HTTP calls.
public sealed class CorrelationHttpMessageHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            if (context.Items.TryGetValue(CorrelationContext.TraceParentItemKey, out var traceParent)
                && traceParent is string traceParentValue
                && !string.IsNullOrWhiteSpace(traceParentValue))
            {
                request.Headers.TryAddWithoutValidation(TraceParent.HeaderName, traceParentValue);
            }

            if (context.Items.TryGetValue(CorrelationContext.CorrelationIdItemKey, out var correlationId)
                && correlationId is string correlationIdValue
                && !string.IsNullOrWhiteSpace(correlationIdValue))
            {
                request.Headers.TryAddWithoutValidation(CorrelationContext.CorrelationIdHeaderName, correlationIdValue);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
