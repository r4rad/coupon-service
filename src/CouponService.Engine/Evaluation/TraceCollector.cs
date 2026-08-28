using System.Collections.Immutable;
using CouponService.Engine.Ast;

namespace CouponService.Engine.Evaluation;

public sealed class TraceCollector
{
    private readonly List<NearMissRecord> _nearMisses = [];
    private readonly List<TraceNodeRecord>? _nodes;

    public TraceCollector(bool captureFullTrace)
    {
        if (captureFullTrace)
        {
            _nodes = [];
        }
    }

    public IReadOnlyList<NearMissRecord> NearMisses => _nearMisses;

    public void RecordNearMiss(NearMissRecord nearMiss) => _nearMisses.Add(nearMiss);

    public void RecordNode(string path, string operatorName, Value result, bool? passed) =>
        _nodes?.Add(new TraceNodeRecord(path, operatorName, result, passed));

    public EvaluationTrace ToEvaluationTrace() =>
        new(
            _nodes?.ToImmutableArray() ?? ImmutableArray<TraceNodeRecord>.Empty,
            _nearMisses.ToImmutableArray());
}
