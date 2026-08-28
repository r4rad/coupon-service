using CouponService.Engine.Parsing;

namespace CouponService.EngineTests.Parsing;

public sealed class ParseBudgetTests
{
    [Fact]
    public void Spend_throws_when_node_count_budget_is_exceeded()
    {
        var budget = new ParseBudget(maxNodes: 2, maxDepth: 10);
        budget.Spend();
        budget.Spend();

        var exception = Assert.Throws<PolicyBudgetException>(() => budget.Spend());

        Assert.Equal("node-count", exception.LimitKind);
        Assert.Equal(2, exception.MaxNodes);
    }

    [Fact]
    public void Deeper_throws_when_nesting_depth_budget_is_exceeded()
    {
        var budget = new ParseBudget(maxNodes: 100, maxDepth: 1);
        var depthOne = budget.Deeper();

        var exception = Assert.Throws<PolicyBudgetException>(() => depthOne.Deeper());

        Assert.Equal("nesting-depth", exception.LimitKind);
        Assert.Equal(1, exception.MaxDepth);
    }
}
