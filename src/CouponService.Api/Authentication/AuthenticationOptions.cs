namespace CouponService.Api.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public JwtBearerOptions Jwt { get; init; } = new();

    public TestTokenOptions TestToken { get; init; } = new();
}

public sealed class JwtBearerOptions
{
    public string Authority { get; init; } = "https://login.microsoftonline.com/{tenant-id}/v2.0";

    /// <summary>
    /// Application ID URI clients use to request a token. Accepted as an audience because
    /// version 1 tokens echo the requested resource.
    /// </summary>
    public string Audience { get; init; } = "api://coupon-service";

    /// <summary>
    /// Client id of the Coupon Service app registration. Version 2 tokens always carry this
    /// GUID in <c>aud</c> rather than the Application ID URI, so it must be accepted too or
    /// every Entra caller is rejected (AC-7.6).
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    public string Issuer { get; init; } = "https://login.microsoftonline.com/{tenant-id}/v2.0";

    /// <summary>
    /// Service principal object ids allowed to call reservation routes when Entra omits the
    /// <c>roles</c> claim on managed-identity tokens (AC-7.7). App role assignment still required.
    /// </summary>
    public string[] TrustedRedeemPrincipalIds { get; init; } = [];

    public IReadOnlyCollection<string> ValidAudiences() =>
        new[] { Audience, ClientId }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}

public sealed class TestTokenOptions
{
    public bool Enabled { get; init; }

    public string SigningKey { get; init; } = "local-dev-only-symmetric-key-min-32-chars!";

    public string Issuer { get; init; } = "coupon-service-test";

    public string Audience { get; init; } = "coupon-service";
}
