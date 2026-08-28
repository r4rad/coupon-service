using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CouponService.Api.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace CouponService.ApiTests.Auth;

internal static class TestTokenFactory
{
    internal const string SigningKey = "local-dev-only-symmetric-key-min-32-chars!";

    internal const string Issuer = "coupon-service-test";

    internal const string Audience = "coupon-service";

    internal static string CreateToken(params string[] roles)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToArray();

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static IReadOnlyDictionary<string, string?> TestAuthenticationConfiguration =>
        new Dictionary<string, string?>
        {
            [$"{AuthenticationOptions.SectionName}:TestToken:Enabled"] = "true",
            [$"{AuthenticationOptions.SectionName}:TestToken:SigningKey"] = SigningKey,
            [$"{AuthenticationOptions.SectionName}:TestToken:Issuer"] = Issuer,
            [$"{AuthenticationOptions.SectionName}:TestToken:Audience"] = Audience,
        };
}
