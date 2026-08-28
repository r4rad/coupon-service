using System.Collections.Immutable;

namespace CouponService.Engine.Ast;

public readonly record struct Value(
    ValueKind Kind,
    decimal Number,
    string? Text,
    bool Bool,
    ImmutableArray<Value> List) : IEquatable<Value>
{
    public static Value Of(decimal number) =>
        new(ValueKind.Number, number, null, default, default);

    public static Value Of(string text) =>
        new(ValueKind.Text, default, text, default, default);

    public static Value Of(bool value) =>
        new(ValueKind.Bool, default, null, value, default);

    public static Value Of(ImmutableArray<Value> list) =>
        new(ValueKind.List, default, null, default, list);

    public static Value Of(IEnumerable<Value> list) =>
        Of(list.ToImmutableArray());

    public decimal GetNumber() =>
        Kind switch
        {
            ValueKind.Number => Number,
            _ => throw new ValueKindMismatchException(Kind, ValueKind.Number),
        };

    public string GetText() =>
        Kind switch
        {
            ValueKind.Text when Text is not null => Text,
            ValueKind.Text => throw new ValueKindMismatchException(Kind, ValueKind.Text),
            _ => throw new ValueKindMismatchException(Kind, ValueKind.Text),
        };

    public bool GetBool() =>
        Kind switch
        {
            ValueKind.Bool => Bool,
            _ => throw new ValueKindMismatchException(Kind, ValueKind.Bool),
        };

    public ImmutableArray<Value> GetList() =>
        Kind switch
        {
            ValueKind.List => List,
            _ => throw new ValueKindMismatchException(Kind, ValueKind.List),
        };

    public bool Equals(Value other) =>
        Kind switch
        {
            ValueKind.Number => other.Kind == ValueKind.Number && Number == other.Number,
            ValueKind.Text => other.Kind == ValueKind.Text && Text == other.Text,
            ValueKind.Bool => other.Kind == ValueKind.Bool && Bool == other.Bool,
            ValueKind.List => other.Kind == ValueKind.List && List.SequenceEqual(other.List),
            _ => other.Kind == Kind,
        };

    public override int GetHashCode() =>
        Kind switch
        {
            ValueKind.Number => HashCode.Combine(Kind, Number),
            ValueKind.Text => HashCode.Combine(Kind, Text),
            ValueKind.Bool => HashCode.Combine(Kind, Bool),
            ValueKind.List => List.Aggregate(
                Kind.GetHashCode(),
                static (hash, item) => HashCode.Combine(hash, item)),
            _ => Kind.GetHashCode(),
        };
}
