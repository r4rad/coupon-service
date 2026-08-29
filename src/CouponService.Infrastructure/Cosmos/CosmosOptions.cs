namespace CouponService.Infrastructure.Cosmos;

/// <summary>
/// Cosmos account settings. Database <c>coupons</c> with containers policies, redemptions, orders (design § Cosmos containers, P-10).
/// </summary>
public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = "coupons";

    public string PoliciesContainerName { get; init; } = "policies";

    public string RedemptionsContainerName { get; init; } = "redemptions";

    public string OrdersContainerName { get; init; } = "orders";
}
