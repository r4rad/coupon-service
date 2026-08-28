using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OrderApi.Catalog;
using OrderApi.Orders;
using OrderApi.Services;

namespace OrderApi.Controllers.V1;

[ApiController]
[Route("v1/pizzas")]
[Tags("Catalog")]
public sealed class PizzasController(IPizzaCatalog catalog) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List pizzas")]
    [ProducesResponseType(typeof(PizzaCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult<PizzaCatalogResponse> ListAsync()
    {
        var snapshot = catalog.GetSnapshot();
        if (Request.Headers.IfNoneMatch.Any(value =>
                string.Equals(value, snapshot.ETag, StringComparison.Ordinal)))
        {
            Response.Headers.ETag = snapshot.ETag;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = snapshot.ETag;
        return Ok(new PizzaCatalogResponse(
            snapshot.Currency,
            snapshot.Pizzas
                .Select(pizza => new PizzaResponse(pizza.Id, pizza.Name, pizza.UnitPrice, pizza.Vegetarian))
                .ToArray()));
    }
}

[ApiController]
[Route("v1/orders")]
[Tags("Orders")]
public sealed class OrdersController(IOrderCheckoutService checkout) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Place an order")]
    [EndpointDescription(
        "Discards any client-supplied total, re-prices through the Coupon Service when a code is present, persists the authoritative total, then confirms the reservation.")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> CreateAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await checkout
            .PlaceOrderAsync(
                new CreateOrderCommand(
                    request.CustomerId,
                    request.Lines
                        .Select(line => new CreateOrderLineRequest(line.PizzaId, line.Quantity))
                        .ToArray(),
                    request.CouponCode,
                    request.ClientTotal,
                    request.CouponPolicy),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return StatusCode(
                result.FailureStatusCode ?? StatusCodes.Status400BadRequest,
                new ProblemDetails
                {
                    Status = result.FailureStatusCode,
                    Title = result.FailureTitle,
                });
        }

        return Created($"/v1/orders/{result.Order!.OrderId}", ToResponse(result.Order));
    }

    [HttpGet("{orderId}")]
    [EndpointSummary("Get an order")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetAsync(
        string orderId,
        [FromServices] IOrderRepository orders,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(order));
    }

    private static OrderResponse ToResponse(OrderRecord order) =>
        new(
            order.OrderId,
            order.CustomerId,
            order.Currency,
            order.Subtotal,
            order.Discount,
            order.Total,
            order.CouponCode,
            order.CouponRejectionReason,
            order.CouponServiceDegraded,
            order.Lines.Select(line => new OrderLineResponse(
                line.LineId,
                line.PizzaId,
                line.Category,
                line.UnitPrice,
                line.Quantity,
                line.LineTotal)).ToArray(),
            order.CreatedAt);

    public sealed class CreateOrderRequest
    {
        [Required]
        public string CustomerId { get; init; } = string.Empty;

        [Required]
        [MinLength(1)]
        public IReadOnlyList<OrderLineRequest> Lines { get; init; } = [];

        public string? CouponCode { get; init; }

        public decimal? ClientTotal { get; init; }

        public CouponCheckoutPolicy CouponPolicy { get; init; } = CouponCheckoutPolicy.AllowWithoutDiscount;
    }

    public sealed class OrderLineRequest
    {
        [Required]
        public string PizzaId { get; init; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int Quantity { get; init; }
    }
}

public sealed record PizzaCatalogResponse(
    string Currency,
    IReadOnlyList<PizzaResponse> Pizzas);

public sealed record PizzaResponse(
    string Id,
    string Name,
    decimal UnitPrice,
    bool Vegetarian);

public sealed record OrderResponse(
    string OrderId,
    string CustomerId,
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string? CouponCode,
    string? CouponRejectionReason,
    bool CouponServiceDegraded,
    IReadOnlyList<OrderLineResponse> Lines,
    DateTimeOffset CreatedAt);

public sealed record OrderLineResponse(
    string LineId,
    string PizzaId,
    string Category,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
