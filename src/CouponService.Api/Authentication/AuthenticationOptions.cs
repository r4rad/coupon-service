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

    public string Audience { get; init; } = "api://coupon-service";

    public string Issuer { get; init; } = "https://login.microsoftonline.com/{tenant-id}/v2.0";
}

public sealed class TestTokenOptions
{
    public bool Enabled { get; init; }

    public string SigningKey { get; init; } = "local-dev-only-symmetric-key-min-32-chars!";

    public string Issuer { get; init; } = "coupon-service-test";

    public string Audience { get; init; } = "coupon-service";
}
