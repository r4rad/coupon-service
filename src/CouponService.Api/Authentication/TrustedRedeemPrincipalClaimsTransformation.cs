using System.Security.Claims;

namespace CouponService.Api.Authentication;

/// <summary>
/// Entra managed-identity tokens for custom APIs authenticate but often omit the <c>roles</c>
/// claim even when <c>Coupon.Redeem</c> is assigned. Map trusted Order API principals (AC-7.7).
/// </summary>
internal static class TrustedRedeemPrincipalClaimsTransformation
{
    internal static void Apply(ClaimsPrincipal? principal, IReadOnlyCollection<string> trustedPrincipalIds)
    {
        if (principal?.Identity is not ClaimsIdentity identity || trustedPrincipalIds.Count == 0)
        {
            return;
        }

        if (identity.HasClaim(static c => c.Type == "roles" && c.Value == AuthorizationPolicies.Redeem))
        {
            return;
        }

        var principalId = identity.FindFirst("oid")?.Value
            ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(principalId))
        {
            return;
        }

        foreach (var trustedId in trustedPrincipalIds)
        {
            if (string.IsNullOrWhiteSpace(trustedId))
            {
                continue;
            }

            if (string.Equals(principalId, trustedId, StringComparison.OrdinalIgnoreCase))
            {
                identity.AddClaim(new Claim("roles", AuthorizationPolicies.Redeem));
                return;
            }
        }
    }
}
