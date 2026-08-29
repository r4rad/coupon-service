using CouponService.Api.Contracts.Preview;
using CouponService.Api.Mapping;
using CouponService.Api.Options;
using CouponService.Application.Policies;
using CouponService.Application.Preview;
using CouponService.Application.Pricing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/coupons")]
[Tags("Coupons")]
public sealed class CouponsController(
    ICouponPreviewService preview,
    IPriceCalculator calculator,
    IOptions<CouponServiceOptions> options,
    IServiceProvider services) : ControllerBase
{
    [HttpPost("preview")]
    [EndpointSummary("Preview coupon pricing")]
    [EndpointDescription(
        "Advisory evaluation against a basket. Omit code to evaluate automatic policies. " +
        "Returns 200 for applied and rejected outcomes with a full price breakdown. Never reserves or writes.")]
    [ProducesResponseType(typeof(PreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreviewResponse>> PreviewAsync(
        [FromBody] PreviewRequest request,
        CancellationToken cancellationToken)
    {
        var cart = PreviewMapping.ToCart(request.Cart);
        var customer = PreviewMapping.ToCustomer(request);

        // AC-6.7: empty code selects automatic policies rather than a coded lookup.
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            var automatic = services.GetService<IAutomaticPolicyPreviewService>();
            if (automatic is null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Automatic preview unavailable",
                    Detail = "This host does not expose automatic policy preview.",
                });
            }

            var decision = await automatic
                .PreviewWithoutCodeAsync(cart, customer, cancellationToken)
                .ConfigureAwait(false);
            var breakdown = calculator.Calculate(cart, decision);
            return Ok(PreviewMapping.ToResponse(new PreviewResult(decision, breakdown), options.Value.Currency));
        }

        var result = await preview.PreviewAsync(
                request.Code,
                cart,
                customer,
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(PreviewMapping.ToResponse(result, options.Value.Currency));
    }
}
