using CouponService.Engine.Ast;

namespace CouponService.Engine.Compilation;

internal static class CompilePaths
{
    internal static string FactPath(string path) => $"{path}.fact";

    internal static string CompareRoot(string path, CompareOp op) =>
        $"{path}.{ToOperatorName(op)}";

    internal static string LogicalOperand(string path, LogicalOp op, int index) =>
        $"{path}.{ToOperatorName(op)}[{index}]";

    internal static string CompareOperand(string path, CompareOp op, int index) =>
        $"{path}.{ToOperatorName(op)}[{index}]";

    internal static string MembershipSubject(string path, MembershipOp op) =>
        $"{path}.{ToOperatorName(op)}[0]";

    internal static string MembershipSetMember(string path, MembershipOp op, int index) =>
        $"{path}.{ToOperatorName(op)}[1][{index}]";

    internal static string MembershipBetweenBound(string path, int index) =>
        $"{path}.between[{index + 1}]";

    internal static string QuantifierPath(string path, QuantifierOp op) =>
        $"{path}.{ToOperatorName(op)}";

    internal static string QuantifierWherePath(string path, QuantifierOp op) =>
        $"{QuantifierPath(path, op)}.where";

    internal static string AggregatePath(string path, AggregateOp op) =>
        $"{path}.{ToOperatorName(op)}";

    internal static string AggregateWherePath(string path, AggregateOp op) =>
        $"{AggregatePath(path, op)}.lines.where";

    internal static string ArithmeticOperand(string path, ArithmeticOp op, int index) =>
        $"{path}.{ToOperatorName(op)}[{index}]";

    internal static string ToOperatorName(LogicalOp op) => op switch
    {
        LogicalOp.All => "all",
        LogicalOp.Any => "any",
        LogicalOp.Not => "not",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string ToOperatorName(CompareOp op) => op switch
    {
        CompareOp.Eq => "eq",
        CompareOp.Neq => "neq",
        CompareOp.Gt => "gt",
        CompareOp.Gte => "gte",
        CompareOp.Lt => "lt",
        CompareOp.Lte => "lte",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string ToOperatorName(MembershipOp op) => op switch
    {
        MembershipOp.In => "in",
        MembershipOp.Between => "between",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string ToOperatorName(QuantifierOp op) => op switch
    {
        QuantifierOp.Every => "every",
        QuantifierOp.Some => "some",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string ToOperatorName(AggregateOp op) => op switch
    {
        AggregateOp.Sum => "sum",
        AggregateOp.Count => "count",
        AggregateOp.Min => "min",
        AggregateOp.Max => "max",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    internal static string ToOperatorName(ArithmeticOp op) => op switch
    {
        ArithmeticOp.Add => "add",
        ArithmeticOp.Sub => "sub",
        ArithmeticOp.Mul => "mul",
        ArithmeticOp.MinOf => "minOf",
        ArithmeticOp.MaxOf => "maxOf",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}
