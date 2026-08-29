namespace CouponService.Bdd.Support;

/// <summary>
/// BDD target configuration. Same feature files run against InProcess TestServer today
/// and against an external base URL (APIM) later without editing Gherkin (design §19 / P-9 adjacent).
/// </summary>
public sealed class BddOptions
{
    public const string SectionName = "Bdd";

    /// <summary>InProcess hosts via WebApplicationFactory; Http calls configured base URLs.</summary>
    public string Mode { get; init; } = "InProcess";

    public string CouponServiceBaseUrl { get; init; } = string.Empty;

    public string OrderApiBaseUrl { get; init; } = string.Empty;

    /// <summary>TestToken locally; ClientCredentials once a tenant exists (P-8).</summary>
    public string TokenStrategy { get; init; } = "TestToken";

    public TestTokenOptions TestToken { get; init; } = new();

    public bool IsInProcess =>
        string.Equals(Mode, "InProcess", StringComparison.OrdinalIgnoreCase);
}

public sealed class TestTokenOptions
{
    public string SigningKey { get; init; } = "local-dev-only-symmetric-key-min-32-chars!";

    public string Issuer { get; init; } = "coupon-service-test";

    public string Audience { get; init; } = "coupon-service";
}
