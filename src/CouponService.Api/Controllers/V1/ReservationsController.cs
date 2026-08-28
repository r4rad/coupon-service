using System.ComponentModel.DataAnnotations;
using CouponService.Api.Contracts.Preview;
using CouponService.Api.Mapping;
using CouponService.Api.Options;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/reservations")]
[Tags("Reservations")]
public sealed class ReservationsController(
    ICouponRedeemer redeemer,
    IOptions<CouponServiceOptions> options) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Reserve a coupon for checkout")]
    [EndpointDescription(
        "Authoritative re-price and reserve a use. Returns 201 with the audited breakdown, or 409 with UsageLimitReached when the cap is consumed. Client-supplied totals are ignored.")]
    [ProducesResponseType(typeof(ReservationCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ReservationConflictResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReservationCreatedResponse>> ReserveAsync(
        [FromBody] ReserveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await redeemer.ReserveAsync(
                request.Code,
                request.OrderId,
                PreviewMapping.ToCart(request.Cart),
                new CustomerContext(request.CustomerId, request.ConfirmedOrderCount),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Conflict(new ReservationConflictResponse(result.Reason!.Value));
        }

        var response = ToCreatedResponse(request.OrderId, result, options.Value.Currency);
        return Created($"/v1/reservations/{request.OrderId}", response);
    }

    [HttpPost("{orderId}/confirm")]
    [EndpointSummary("Confirm a reservation")]
    [EndpointDescription("Commits a reserved use after the order is persisted. Idempotent for the same orderId.")]
    [ProducesResponseType(typeof(ReservationTransitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ReservationStateConflictResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationTransitionResponse>> ConfirmAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        var result = await redeemer.ConfirmAsync(orderId, cancellationToken).ConfigureAwait(false);

        if (result.Redemption is null)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return Conflict(new ReservationStateConflictResponse(result.Redemption.State));
        }

        return Ok(ToTransitionResponse(result.Redemption));
    }

    [HttpPost("{orderId}/release")]
    [EndpointSummary("Release a reservation")]
    [EndpointDescription("Returns a reserved use when checkout fails. Idempotent for the same orderId.")]
    [ProducesResponseType(typeof(ReservationTransitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ReservationStateConflictResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationTransitionResponse>> ReleaseAsync(
        string orderId,
        [FromBody] ReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await redeemer
            .ReleaseAsync(orderId, request.Reason, cancellationToken)
            .ConfigureAwait(false);

        if (result.Redemption is null)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            return Conflict(new ReservationStateConflictResponse(result.Redemption.State));
        }

        return Ok(ToTransitionResponse(result.Redemption));
    }

    private static ReservationCreatedResponse ToCreatedResponse(
        string orderId,
        ReservationResult result,
        string currency)
    {
        var breakdown = result.Breakdown!;
        return new ReservationCreatedResponse(
            orderId,
            new PricingResponse(
                currency,
                breakdown.Lines
                    .Select(line => new LinePricingResponse(line.LineId, line.Amount))
                    .ToArray(),
                breakdown.Subtotal,
                breakdown.Discount,
                breakdown.Total),
            result.Redemption!.PolicyContentHash);
    }

    private static ReservationTransitionResponse ToTransitionResponse(RedemptionRecord redemption) =>
        new(redemption.OrderId, redemption.State);
}

public sealed class ReserveRequest
{
    [Required]
    public string OrderId { get; init; } = string.Empty;

    [Required]
    public string Code { get; init; } = string.Empty;

    [Required]
    public string CustomerId { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ConfirmedOrderCount { get; init; }

    [Required]
    public CartRequest Cart { get; init; } = new();
}

public sealed class ReleaseRequest
{
    [Required]
    public string Reason { get; init; } = string.Empty;
}

public sealed record ReservationCreatedResponse(
    string OrderId,
    PricingResponse Pricing,
    string PolicyContentHash);

public sealed record ReservationTransitionResponse(
    string OrderId,
    RedemptionState State);

public sealed record ReservationConflictResponse(RejectionReason Reason);

public sealed record ReservationStateConflictResponse(RedemptionState State);
