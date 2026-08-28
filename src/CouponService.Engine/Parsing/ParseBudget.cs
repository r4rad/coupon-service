namespace CouponService.Engine.Parsing;

public sealed class ParseBudget
{
    private readonly ParseBudgetCounters _counters;

    public ParseBudget(int maxNodes, int maxDepth)
        : this(maxNodes, maxDepth, new ParseBudgetCounters(), currentDepth: 0)
    {
    }

    private ParseBudget(int maxNodes, int maxDepth, ParseBudgetCounters counters, int currentDepth)
    {
        MaxNodes = maxNodes;
        MaxDepth = maxDepth;
        _counters = counters;
        CurrentDepth = currentDepth;
    }

    public int MaxNodes { get; }

    public int MaxDepth { get; }

    public int CurrentDepth { get; }

    public int NodesSpent => _counters.NodesSpent;

    public void Spend()
    {
        _counters.NodesSpent++;
        if (_counters.NodesSpent > MaxNodes)
        {
            throw new PolicyBudgetException(MaxNodes, MaxDepth, "node-count");
        }
    }

    public ParseBudget Deeper()
    {
        var nextDepth = CurrentDepth + 1;
        if (nextDepth > MaxDepth)
        {
            throw new PolicyBudgetException(MaxNodes, MaxDepth, "nesting-depth");
        }

        return new ParseBudget(MaxNodes, MaxDepth, _counters, nextDepth);
    }

    private sealed class ParseBudgetCounters
    {
        public int NodesSpent { get; set; }
    }
}
