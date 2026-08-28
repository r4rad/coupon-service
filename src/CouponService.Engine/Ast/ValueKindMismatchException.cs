namespace CouponService.Engine.Ast;

public sealed class ValueKindMismatchException(ValueKind actual, ValueKind expected)
    : Exception($"Expected value kind {expected} but found {actual}.")
{
    public ValueKind Actual { get; } = actual;

    public ValueKind Expected { get; } = expected;
}
