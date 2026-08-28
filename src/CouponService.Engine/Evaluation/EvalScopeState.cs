namespace CouponService.Engine.Evaluation;

internal sealed class EvalScopeState
{
    internal Dictionary<string, Ast.Value> FactMemo { get; } = new(StringComparer.Ordinal);

    internal required TraceCollector Trace { get; init; }

    internal bool CaptureFullTrace { get; init; }
}
