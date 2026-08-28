namespace CouponService.Engine.Parsing;

public sealed class PolicySyntaxException(string path, string message)
    : Exception(message)
{
    public string Path { get; } = path;
}
