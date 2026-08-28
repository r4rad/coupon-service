using OrderApi.Catalog;
using OrderApi.Clients;
using OrderApi.Orders;

namespace OrderApi.Services;

public sealed record CreateOrderLineRequest(string PizzaId, int Quantity);

public sealed record CreateOrderCommand(
    string CustomerId,
    IReadOnlyList<CreateOrderLineRequest> Lines,
    string? CouponCode,
    decimal? ClientTotal,
    CouponCheckoutPolicy CouponPolicy,
    int ConfirmedOrderCount = 0);

public sealed record CreateOrderResult(
    bool Succeeded,
    OrderRecord? Order,
    int? FailureStatusCode,
    string? FailureTitle);

public interface IOrderCheckoutService
{
    Task<CreateOrderResult> PlaceOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
}

public sealed class OrderCheckoutService(
    IPizzaCatalog catalog,
    ICouponServiceClient couponService,
    IOrderRepository orders,
    IClock clock,
    ILogger<OrderCheckoutService> logger) : IOrderCheckoutService
{
    public async Task<CreateOrderResult> PlaceOrderAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var snapshot = catalog.GetSnapshot();
        var builtLines = BuildLines(command.Lines, snapshot);
        if (builtLines is null)
        {
            return new CreateOrderResult(false, null, StatusCodes.Status400BadRequest, "Unknown pizza in basket.");
        }

        var subtotal = builtLines.Sum(line => line.LineTotal);
        var orderId = Guid.NewGuid().ToString("N");
        var reserved = false;
        string? reservationOrderId = null;

        decimal discount = 0m;
        decimal total = subtotal;
        string? rejectionReason = null;
        var degraded = false;

        if (!string.IsNullOrWhiteSpace(command.CouponCode))
        {
            reservationOrderId = orderId;
            var reservation = await couponService
                .ReserveAsync(
                    new CouponReservationRequest(
                        orderId,
                        command.CouponCode,
                        command.CustomerId,
                        command.ConfirmedOrderCount,
                        builtLines.Select(line => new CouponCartLine(
                            line.LineId,
                            line.PizzaId,
                            line.Category,
                            line.UnitPrice,
                            line.Quantity)).ToArray()),
                    cancellationToken)
                .ConfigureAwait(false);

            if (reservation.Unreachable)
            {
                degraded = true;
                logger.LogWarning(
                    "CouponServiceUnavailable at checkout for order {OrderId}",
                    orderId);
            }
            else if (reservation.Succeeded && reservation.Pricing is not null)
            {
                reserved = true;
                discount = reservation.Pricing.Discount;
                total = reservation.Pricing.Total;
            }
            else
            {
                rejectionReason = reservation.RejectionReason ?? "Rejected";
                if (command.CouponPolicy is CouponCheckoutPolicy.RequireDiscount)
                {
                    return new CreateOrderResult(
                        false,
                        null,
                        StatusCodes.Status409Conflict,
                        rejectionReason);
                }
            }
        }

        _ = command.ClientTotal;

        var order = new OrderRecord(
            orderId,
            command.CustomerId,
            snapshot.Currency,
            subtotal,
            discount,
            total,
            command.CouponCode,
            rejectionReason,
            degraded,
            builtLines,
            clock.UtcNow);

        try
        {
            await orders.SaveAsync(order, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (reserved && reservationOrderId is not null)
            {
                await couponService
                    .ReleaseAsync(reservationOrderId, "order-persistence-failed", cancellationToken)
                    .ConfigureAwait(false);
            }

            throw;
        }

        if (reserved && reservationOrderId is not null)
        {
            await couponService.ConfirmAsync(reservationOrderId, cancellationToken).ConfigureAwait(false);
        }

        return new CreateOrderResult(true, order, null, null);
    }

    private static IReadOnlyList<OrderLine>? BuildLines(
        IReadOnlyList<CreateOrderLineRequest> requestedLines,
        PizzaCatalogSnapshot snapshot)
    {
        var lines = new List<OrderLine>();
        var index = 0;
        foreach (var requested in requestedLines)
        {
            var pizza = snapshot.Pizzas.FirstOrDefault(entry =>
                string.Equals(entry.Id, requested.PizzaId, StringComparison.OrdinalIgnoreCase));
            if (pizza is null || requested.Quantity < 1)
            {
                return null;
            }

            index++;
            var category = pizza.Vegetarian ? "classic" : "meat";
            lines.Add(new OrderLine(
                $"line-{index}",
                pizza.Id,
                category,
                pizza.UnitPrice,
                requested.Quantity,
                pizza.UnitPrice * requested.Quantity));
        }

        return lines;
    }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
