namespace CouponService.Application.Policies;

public static class PolicyPartitionKey
{
    public static string ForCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code;
    }

    public static string ForAutomatic(string policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);
        return $"AUTO#{policyId}";
    }
}
