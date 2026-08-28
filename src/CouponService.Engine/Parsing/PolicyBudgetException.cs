namespace CouponService.Engine.Parsing;

public sealed class PolicyBudgetException(int maxNodes, int maxDepth, string limitKind)
    : Exception($"Policy exceeds configured {limitKind} budget (max nodes: {maxNodes}, max depth: {maxDepth}).")
{
    public int MaxNodes { get; } = maxNodes;

    public int MaxDepth { get; } = maxDepth;

    public string LimitKind { get; } = limitKind;
}
