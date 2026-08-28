using System.Collections.Immutable;
using CouponService.Engine.Ast;

namespace CouponService.EngineTests.Ast;

public sealed class ValueTests
{
    [Fact]
    public void GetNumber_throws_when_value_is_not_a_number()
    {
        var value = Value.Of("Vegetarian");

        var exception = Assert.Throws<ValueKindMismatchException>(() => value.GetNumber());

        Assert.Equal(ValueKind.Text, exception.Actual);
        Assert.Equal(ValueKind.Number, exception.Expected);
    }

    [Fact]
    public void GetText_throws_when_value_is_not_text()
    {
        var value = Value.Of(25.00m);

        var exception = Assert.Throws<ValueKindMismatchException>(() => value.GetText());

        Assert.Equal(ValueKind.Number, exception.Actual);
        Assert.Equal(ValueKind.Text, exception.Expected);
    }

    [Fact]
    public void GetBool_throws_when_value_is_not_a_boolean()
    {
        var value = Value.Of(1.00m);

        Assert.Throws<ValueKindMismatchException>(() => value.GetBool());
    }

    [Fact]
    public void GetList_throws_when_value_is_not_a_list()
    {
        var value = Value.Of(true);

        Assert.Throws<ValueKindMismatchException>(() => value.GetList());
    }

    [Fact]
    public void List_values_use_structural_equality()
    {
        var left = Value.Of(
        [
            Value.Of("Saturday"),
            Value.Of("Sunday"),
        ]);
        var right = Value.Of(
        [
            Value.Of("Saturday"),
            Value.Of("Sunday"),
        ]);
        var differentOrder = Value.Of(
        [
            Value.Of("Sunday"),
            Value.Of("Saturday"),
        ]);

        Assert.Equal(left, right);
        Assert.NotEqual(left, differentOrder);
    }

    [Fact]
    public void Number_values_preserve_decimal_precision()
    {
        const decimal unitPrice = 9.50m;

        var value = Value.Of(unitPrice);

        Assert.Equal(unitPrice, value.GetNumber());
    }
}
