using CouponService.Api.Contracts.Preview;
using CouponService.Api.Mapping;
using CouponService.Api.Options;
using CouponService.Application.Preview;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/coupons")]
[Tags("Coupons")]
public sealed class CouponsController(
    ICouponPreviewService preview,
    IOptions<CouponServiceOptions> options) : ControllerBase
{
    [HttpPost("preview")]
    [EndpointSummary("Preview coupon pricing")]
    [EndpointDescription(
        "Advisory evaluation against a basket. Returns 200 for applied and rejected outcomes with a full price breakdown. Never reserves or writes.")]
    [ProducesResponseType(typeof(PreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreviewResponse>> PreviewAsync(
        [FromBody] PreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await preview.PreviewAsync(
                request.Code,
                PreviewMapping.ToCart(request.Cart),
                PreviewMapping.ToCustomer(request),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(PreviewMapping.ToResponse(result, options.Value.Currency));
    }
}
