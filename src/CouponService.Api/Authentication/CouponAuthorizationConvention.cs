using CouponService.Api.Controllers.V1;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace CouponService.Api.Authentication;

// AC-7.3 / AC-7.4: role requirements live in application code, not only at the gateway.
internal sealed class CouponAuthorizationConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (controller.ControllerType == typeof(ReservationsController))
        {
            controller.Filters.Add(new AuthorizeFilter(AuthorizationPolicies.Redeem));
            return;
        }

        if (controller.ControllerType == typeof(AdminPoliciesController)
            || controller.ControllerType == typeof(PolicyEngineManifestController))
        {
            controller.Filters.Add(new AuthorizeFilter(AuthorizationPolicies.Admin));
        }
    }
}
