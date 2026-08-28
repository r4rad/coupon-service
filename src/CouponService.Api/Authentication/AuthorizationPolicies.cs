using Microsoft.AspNetCore.Authorization;

namespace CouponService.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string Redeem = "Coupon.Redeem";

    public const string Admin = "Coupon.Admin";

    public static void AddCouponAuthorizationPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(Redeem, policy => policy.RequireRole(Redeem));
        options.AddPolicy(Admin, policy => policy.RequireRole(Admin));
    }
}
