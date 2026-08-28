namespace CouponService.Api.Observability;

public static class CorrelationContext
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";

    public const string CorrelationIdItemKey = "CorrelationId";

    public const string TraceParentItemKey = "TraceParent";
}
