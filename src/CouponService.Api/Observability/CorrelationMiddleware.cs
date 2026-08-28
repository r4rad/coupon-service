namespace CouponService.Api.Observability;

// AC-8.2: accept inbound W3C traceparent or generate one, echo it, and keep caller correlation ids.
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        var traceParent = ResolveTraceParent(context, correlationId);

        context.Items[CorrelationContext.CorrelationIdItemKey] = correlationId;
        context.Items[CorrelationContext.TraceParentItemKey] = traceParent;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationContext.CorrelationIdHeaderName] = correlationId;
        context.Response.Headers[TraceParent.HeaderName] = traceParent;

        await next(context).ConfigureAwait(false);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationContext.CorrelationIdHeaderName, out var correlationHeader)
            && !string.IsNullOrWhiteSpace(correlationHeader))
        {
            return correlationHeader.ToString();
        }

        if (context.Request.Headers.TryGetValue(TraceParent.HeaderName, out var traceParentHeader)
            && TraceParent.TryParseTraceId(traceParentHeader.ToString(), out var traceId))
        {
            return traceId;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string ResolveTraceParent(HttpContext context, string correlationId)
    {
        if (context.Request.Headers.TryGetValue(TraceParent.HeaderName, out var traceParentHeader)
            && !string.IsNullOrWhiteSpace(traceParentHeader)
            && TraceParent.TryParseTraceId(traceParentHeader.ToString(), out _))
        {
            return traceParentHeader.ToString();
        }

        var traceId = correlationId.Length == 32 && IsHex(correlationId)
            ? correlationId
            : Guid.NewGuid().ToString("N");

        return TraceParent.Create(traceId);
    }

    private static bool IsHex(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
