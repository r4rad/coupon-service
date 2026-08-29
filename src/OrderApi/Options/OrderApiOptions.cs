namespace OrderApi.Options;

public sealed class OrderApiOptions
{
    public const string SectionName = "OrderApi";

    public string CouponServiceBaseUrl { get; init; } = "https://localhost:7081";

    /// <summary>
    /// Local/dev bearer for the Coupon Service hop. Unused when <see cref="UseManagedIdentity"/> is true (AC-7.7).
    /// </summary>
    public string CouponServiceToken { get; init; } = string.Empty;

    /// <summary>
    /// When true, acquire a token via the host managed-identity endpoint — no shared secret (AC-7.7).
    /// </summary>
    public bool UseManagedIdentity { get; init; }

    /// <summary>
    /// Application ID URI of the Coupon Service API; used as the IMDS / identity-endpoint resource.
    /// </summary>
    public string CouponServiceResource { get; init; } = "api://coupon-service";

    /// <summary>
    /// OAuth scope documented for reviewers; identity endpoint uses <see cref="CouponServiceResource"/>.
    /// </summary>
    public string CouponServiceScope { get; init; } = "api://coupon-service/.default";

    public string PizzasFilePath { get; init; } = "data/pizzas.json";
}
