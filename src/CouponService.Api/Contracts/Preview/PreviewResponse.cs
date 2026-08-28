using CouponService.Application.Pricing;

namespace CouponService.Api.Contracts.Preview;

public sealed record PreviewResponse(
    CouponStatus Status,
    RejectionReason? Reason,
    NearMissHintResponse? Hint,
    PricingResponse Pricing,
    string? PolicyContentHash);

public sealed record NearMissHintResponse(
    decimal Shortfall,
    string Message);

public sealed record PricingResponse(
    string Currency,
    IReadOnlyList<LinePricingResponse> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Total);

public sealed record LinePricingResponse(
    string LineId,
    decimal Amount);
