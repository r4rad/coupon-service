using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CouponService.Api.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace CouponService.Bdd.Support;

internal sealed class TokenProvider(BddOptions options)
{
    internal string CreateAdminToken() => CreateToken(AuthorizationPolicies.Admin);

    internal string CreateRedeemToken() => CreateToken(AuthorizationPolicies.Redeem);

    /// <summary>Customer JWT without Coupon.Redeem — reservations must return 403.</summary>
    internal string CreateCustomerToken() => CreateToken();

    internal string CreateToken(params string[] roles)
    {
        if (!string.Equals(options.TokenStrategy, "TestToken", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Token strategy '{options.TokenStrategy}' is not configured for this run. " +
                "Use TestToken until an Entra tenant exists (P-8).");
        }

        var signingKey = options.TestToken.SigningKey;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToArray();

        var token = new JwtSecurityToken(
            issuer: options.TestToken.Issuer,
            audience: options.TestToken.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal IReadOnlyDictionary<string, string?> AuthenticationConfiguration =>
        new Dictionary<string, string?>
        {
            [$"{AuthenticationOptions.SectionName}:TestToken:Enabled"] = "true",
            [$"{AuthenticationOptions.SectionName}:TestToken:SigningKey"] = options.TestToken.SigningKey,
            [$"{AuthenticationOptions.SectionName}:TestToken:Issuer"] = options.TestToken.Issuer,
            [$"{AuthenticationOptions.SectionName}:TestToken:Audience"] = options.TestToken.Audience,
        };
}
