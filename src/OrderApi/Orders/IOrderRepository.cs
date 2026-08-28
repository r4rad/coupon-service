namespace OrderApi.Orders;

public enum CouponCheckoutPolicy
{
    AllowWithoutDiscount,
    RequireDiscount,
}

public sealed record OrderLine(
    string LineId,
    string PizzaId,
    string Category,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderRecord(
    string OrderId,
    string CustomerId,
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string? CouponCode,
    string? CouponRejectionReason,
    bool CouponServiceDegraded,
    IReadOnlyList<OrderLine> Lines,
    DateTimeOffset CreatedAt);

public interface IOrderRepository
{
    Task SaveAsync(OrderRecord order, CancellationToken cancellationToken = default);

    Task<OrderRecord?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);
}
