using System.Text.Json;
using CouponService.Engine.Parsing;

namespace CouponService.EngineTests.Parsing;

public sealed class PolicyParserSyntaxTests
{
    [Fact]
    public void Parse_rejects_objects_with_more_than_one_property()
    {
        using var document = JsonDocument.Parse("""{ "eq": [1, 1], "fact": "cart.subtotal" }""");

        var exception = Assert.Throws<PolicySyntaxException>(() =>
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 50, maxDepth: 10)));

        Assert.Equal("$", exception.Path);
    }

    [Fact]
    public void Parse_rejects_unknown_operators_with_json_path()
    {
        using var document = JsonDocument.Parse("""{ "unknownOp": [1, 2] }""");

        var exception = Assert.Throws<PolicySyntaxException>(() =>
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 50, maxDepth: 10)));

        Assert.Equal("$", exception.Path);
        Assert.Contains("unknownOp", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_deeply_nested_documents_when_depth_budget_is_exceeded()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "not": {
                "not": {
                  "not": { "fact": "cart.subtotal" }
                }
              }
            }
            """);

        var exception = Assert.Throws<PolicyBudgetException>(() =>
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 100, maxDepth: 2)));

        Assert.Equal("nesting-depth", exception.LimitKind);
    }

    [Fact]
    public void Parse_rejects_wide_documents_when_node_budget_is_exceeded()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "all": [
                { "fact": "cart.subtotal" },
                { "fact": "cart.lineCount" },
                { "fact": "cart.totalQuantity" }
              ]
            }
            """);

        var exception = Assert.Throws<PolicyBudgetException>(() =>
            PolicyParser.Parse(document.RootElement, new ParseBudget(maxNodes: 2, maxDepth: 10)));

        Assert.Equal("node-count", exception.LimitKind);
    }
}
