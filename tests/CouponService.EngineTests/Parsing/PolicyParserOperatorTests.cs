using System.Collections.Immutable;
using System.Text.Json;
using CouponService.Engine.Ast;
using CouponService.Engine.Parsing;

namespace CouponService.EngineTests.Parsing;

public sealed class PolicyParserOperatorTests
{
    [Fact]
    public void Parse_parses_architecture_condition_with_all_required_node_types()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "all": [
                { "gte": [ { "fact": "cart.subtotal" }, 25.00 ] },
                {
                  "every": {
                    "over": "cart.lines",
                    "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] }
                  }
                },
                {
                  "any": [
                    { "eq": [ { "fact": "customer.confirmedOrderCount" }, 0 ] },
                    {
                      "in": [
                        { "fact": "time.localDayOfWeek" },
                        ["Saturday", "Sunday"]
                      ]
                    }
                  ]
                },
                { "sum": { "lines": { "where": { "eq": [ { "fact": "line.category" }, "Vegetarian" ] } } } },
                { "add": [9.50, 12.00] }
              ]
            }
            """);

        var parsed = PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 100, maxDepth: 20));

        var all = Assert.IsType<LogicalExpr>(parsed);
        Assert.Equal(LogicalOp.All, all.Op);
        Assert.Equal(5, all.Operands.Length);

        Assert.IsType<CompareExpr>(all.Operands[0]);
        Assert.IsType<QuantifierExpr>(all.Operands[1]);
        Assert.IsType<LogicalExpr>(all.Operands[2]);
        Assert.IsType<AggregateExpr>(all.Operands[3]);
        Assert.IsType<ArithmeticExpr>(all.Operands[4]);
    }

    [Theory]
    [InlineData("""{ "fact": "cart.subtotal" }""", typeof(FactExpr))]
    [InlineData("""25.00""", typeof(ConstExpr))]
    [InlineData("""true""", typeof(ConstExpr))]
    [InlineData("""{ "all": [true, false] }""", typeof(LogicalExpr))]
    [InlineData("""{ "any": [true, false] }""", typeof(LogicalExpr))]
    [InlineData("""{ "not": true }""", typeof(LogicalExpr))]
    [InlineData("""{ "eq": [1, 1] }""", typeof(CompareExpr))]
    [InlineData("""{ "neq": [1, 2] }""", typeof(CompareExpr))]
    [InlineData("""{ "gt": [2, 1] }""", typeof(CompareExpr))]
    [InlineData("""{ "gte": [2, 1] }""", typeof(CompareExpr))]
    [InlineData("""{ "lt": [1, 2] }""", typeof(CompareExpr))]
    [InlineData("""{ "lte": [1, 2] }""", typeof(CompareExpr))]
    [InlineData("""{ "in": [ { "fact": "time.localDayOfWeek" }, ["Saturday", "Sunday"] ] }""", typeof(MembershipExpr))]
    [InlineData("""{ "between": [5, 1, 10] }""", typeof(MembershipExpr))]
    [InlineData("""{ "every": { "over": "cart.lines", "where": true } }""", typeof(QuantifierExpr))]
    [InlineData("""{ "some": { "over": "cart.lines", "where": true } }""", typeof(QuantifierExpr))]
    [InlineData("""{ "sum": { "lines": { "where": true } } }""", typeof(AggregateExpr))]
    [InlineData("""{ "count": { "lines": { "where": true } } }""", typeof(AggregateExpr))]
    [InlineData("""{ "min": { "lines": { "where": true } } }""", typeof(AggregateExpr))]
    [InlineData("""{ "max": { "lines": { "where": true } } }""", typeof(AggregateExpr))]
    [InlineData("""{ "add": [1, 2, 3] }""", typeof(ArithmeticExpr))]
    [InlineData("""{ "sub": [3, 1] }""", typeof(ArithmeticExpr))]
    [InlineData("""{ "mul": [3, 2] }""", typeof(ArithmeticExpr))]
    [InlineData("""{ "minOf": [3, 2] }""", typeof(ArithmeticExpr))]
    [InlineData("""{ "maxOf": [3, 2] }""", typeof(ArithmeticExpr))]
    public void Parse_round_trips_each_condition_operator(string json, Type expectedType)
    {
        using var document = JsonDocument.Parse(json);

        var parsed = PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 50, maxDepth: 10));

        Assert.IsType(expectedType, parsed);
    }

    [Fact]
    public void Parse_in_membership_expands_constant_list_members()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "in": [
                { "fact": "time.localDayOfWeek" },
                ["Saturday", "Sunday"]
              ]
            }
            """);

        var parsed = Assert.IsType<MembershipExpr>(
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 50, maxDepth: 10)));

        Assert.Equal(MembershipOp.In, parsed.Op);
        Assert.IsType<FactExpr>(parsed.Subject);
        Assert.Equal(2, parsed.Set.Length);
        Assert.Equal("Saturday", ((ConstExpr)parsed.Set[0]).Value.GetText());
        Assert.Equal("Sunday", ((ConstExpr)parsed.Set[1]).Value.GetText());
    }

    [Fact]
    public void Parse_between_membership_preserves_three_operands()
    {
        using var document = JsonDocument.Parse("""{ "between": [5, 1, 10] }""");

        var parsed = Assert.IsType<MembershipExpr>(
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 50, maxDepth: 10)));

        Assert.Equal(MembershipOp.Between, parsed.Op);
        Assert.Equal(2, parsed.Set.Length);
    }

    [Fact]
    public void Parse_number_constants_preserve_decimal_precision()
    {
        using var document = JsonDocument.Parse("""9.50""");

        var parsed = Assert.IsType<ConstExpr>(
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 10, maxDepth: 5)));

        Assert.Equal(9.50m, parsed.Value.GetNumber());
    }
}
