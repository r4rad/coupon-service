using CouponService.Api.Contracts.Preview;
using CouponService.Api.Mapping;
using CouponService.Api.Options;
using CouponService.Application.Preview;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/coupons")]
public sealed class CouponsController(
    ICouponPreviewService preview,
    IOptions<CouponServiceOptions> options) : ControllerBase
{
    [HttpPost("preview")]
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
