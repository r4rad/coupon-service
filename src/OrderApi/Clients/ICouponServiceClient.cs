namespace OrderApi.Clients;

public sealed record CouponCartLine(
    string LineId,
    string PizzaId,
    string Category,
    decimal UnitPrice,
    int Quantity);

public sealed record CouponReservationRequest(
    string OrderId,
    string Code,
    string CustomerId,
    int ConfirmedOrderCount,
    IReadOnlyList<CouponCartLine> Lines);

public sealed record CouponPricing(
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Total);

public sealed record CouponReservationResult(
    bool Succeeded,
    CouponPricing? Pricing,
    string? RejectionReason,
    bool Unreachable);

public interface ICouponServiceClient
{
    Task<CouponReservationResult> ReserveAsync(
        CouponReservationRequest request,
        CancellationToken cancellationToken = default);

    Task ConfirmAsync(string orderId, CancellationToken cancellationToken = default);

    Task ReleaseAsync(string orderId, string reason, CancellationToken cancellationToken = default);
}
