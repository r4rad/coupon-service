using System.Security.Claims;
using CouponService.Api.Authentication;

namespace CouponService.ApiTests.Auth;

public sealed class TrustedRedeemPrincipalClaimsTransformationTests
{
    private const string OrderManagedIdentityPrincipalId = "8b1fa495-835d-4825-a556-d9fe2281c475";

    [Fact]
    public void Trusted_order_managed_identity_without_roles_claim_receives_Coupon_Redeem()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("oid", OrderManagedIdentityPrincipalId),
            new Claim("aud", "189703ee-da8c-4fa4-8c0d-a53f193283f4"),
        ],
        authenticationType: "Bearer");

        TrustedRedeemPrincipalClaimsTransformation.Apply(
            new ClaimsPrincipal(identity),
            [OrderManagedIdentityPrincipalId]);

        Assert.Contains(
            identity.Claims,
            claim => claim.Type == "roles" && claim.Value == AuthorizationPolicies.Redeem);
    }

    [Fact]
    public void Unknown_principal_is_not_granted_Coupon_Redeem()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("oid", "00000000-0000-0000-0000-000000000099"),
        ],
        authenticationType: "Bearer");

        TrustedRedeemPrincipalClaimsTransformation.Apply(
            new ClaimsPrincipal(identity),
            [OrderManagedIdentityPrincipalId]);

        Assert.DoesNotContain(identity.Claims, claim => claim.Type == "roles");
    }

    [Fact]
    public void Existing_Coupon_Redeem_role_is_not_duplicated()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("oid", OrderManagedIdentityPrincipalId),
            new Claim("roles", AuthorizationPolicies.Redeem),
        ],
        authenticationType: "Bearer");

        TrustedRedeemPrincipalClaimsTransformation.Apply(
            new ClaimsPrincipal(identity),
            [OrderManagedIdentityPrincipalId]);

        Assert.Equal(1, identity.Claims.Count(claim => claim.Type == "roles"));
    }
}
