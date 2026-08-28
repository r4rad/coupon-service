extern alias OrderApiHost;

using System.Net;
using System.Net.Http.Json;
using CouponService.Application.Policies;
using CouponService.Application.Redemption;
using CouponService.ApiTests.Preview;
using CouponService.ApiTests.Reservations;
using CouponCheckoutPolicy = OrderApiHost::OrderApi.Orders.CouponCheckoutPolicy;

namespace CouponService.ApiTests.Ordering;

public sealed class OrderingEndpointTests : IClassFixture<OrderingFixture>
{
    private readonly OrderingFixture _fixture;

    public OrderingEndpointTests(OrderingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Authoritative_reprice_ignores_client_total_and_stores_coupon_service_total()
    {
        await SeedSave10PolicyAsync("SAVE10-ORDER", "save10-order");

        using var client = _fixture.CreateOrderClient();
        var response = await client.PostAsJsonAsync("/v1/orders", OrderingRequests.Checkout(
            code: "SAVE10-ORDER",
            clientTotal: 1.00m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(31.00m, body!.Subtotal);
        Assert.Equal(3.10m, body.Discount);
        Assert.Equal(27.90m, body.Total);
        Assert.NotEqual(1.00m, body.Total);

        var stored = await _fixture.OrderFactory.Orders.GetByIdAsync(body.OrderId);
        Assert.NotNull(stored);
        Assert.Equal(27.90m, stored!.Total);
    }

    [Fact]
    public async Task Checkout_places_full_price_order_when_coupon_rejected_under_allow_without_discount()
    {
        await SeedPolicyAsync(PreviewTestDocuments.MinimumOrderDocument);

        using var client = _fixture.CreateOrderClient();
        var response = await client.PostAsJsonAsync("/v1/orders", OrderingRequests.Checkout(
            code: "MIN25",
            clientTotal: 1.00m,
            lines: OrderingRequests.SingleLineBasket()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(19.00m, body!.Subtotal);
        Assert.Equal(0m, body.Discount);
        Assert.Equal(19.00m, body.Total);
        Assert.Equal("MinimumOrderNotMet", body.CouponRejectionReason);
    }

    [Fact]
    public async Task Checkout_returns_409_when_coupon_rejected_under_require_discount()
    {
        await SeedPolicyAsync(PreviewTestDocuments.MinimumOrderDocument);

        using var client = _fixture.CreateOrderClient();
        var response = await client.PostAsJsonAsync("/v1/orders", OrderingRequests.Checkout(
            code: "MIN25",
            clientTotal: 1.00m,
            lines: OrderingRequests.SingleLineBasket(),
            couponPolicy: CouponCheckoutPolicy.RequireDiscount));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_places_full_price_order_when_coupon_service_unreachable()
    {
        var token = Auth.TestTokenFactory.CreateToken(CouponService.Api.Authentication.AuthorizationPolicies.Redeem);
        using var couponFactory = new OrderingCouponServiceFactory();
        _ = couponFactory.CreateClient();
        using var orderFactory = new OrderingOrderApiFactory(
            couponFactory,
            token,
            couponServiceBaseUrlOverride: "http://127.0.0.1:9");

        using var client = orderFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/orders", OrderingRequests.Checkout(
            code: "SAVE10",
            clientTotal: 1.00m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(31.00m, body!.Total);
        Assert.True(body.CouponServiceDegraded);
        Assert.Null(body.CouponRejectionReason);
    }

    [Fact]
    public async Task Get_pizzas_returns_catalog_with_etag()
    {
        using var client = _fixture.CreateOrderClient();
        var response = await client.GetAsync("/v1/pizzas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.ETag?.Tag));
        var body = await response.Content.ReadFromJsonAsync<PizzaCatalogResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Pizzas);
    }

    [Fact]
    public async Task Checkout_releases_reservation_when_persistence_fails()
    {
        var token = Auth.TestTokenFactory.CreateToken(CouponService.Api.Authentication.AuthorizationPolicies.Redeem);
        using var couponFactory = new OrderingCouponServiceFactory();
        await SeedPolicyAsync(couponFactory, ReservationTestDocuments.Save10Document("SAVE10-RELEASE", "save10-release"));
        _ = couponFactory.CreateClient();
        using var orderFactory = new OrderingOrderApiFactory(couponFactory, token);

        string? attemptedOrderId = null;
        orderFactory.Orders.SaveInterceptor = (order, _) =>
        {
            attemptedOrderId = order.OrderId;
            throw new InvalidOperationException("Simulated persistence failure");
        };

        using var client = orderFactory.CreateClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsJsonAsync("/v1/orders", OrderingRequests.Checkout(code: "SAVE10-RELEASE", clientTotal: 1.00m)));

        Assert.False(string.IsNullOrWhiteSpace(attemptedOrderId));
        var redemption = await couponFactory.Redemptions.FindByOrderIdAsync(attemptedOrderId!);
        Assert.NotNull(redemption);
        Assert.Equal(RedemptionState.Released, redemption!.State);
    }

    private async Task SeedSave10PolicyAsync(string code, string policyId)
    {
        await SeedPolicyAsync(_fixture.CouponFactory, ReservationTestDocuments.Save10Document(code, policyId));
    }

    private async Task SeedPolicyAsync(string documentJson)
    {
        await SeedPolicyAsync(_fixture.CouponFactory, documentJson);
    }

    private static async Task SeedPolicyAsync(OrderingCouponServiceFactory factory, string documentJson)
    {
        var record = PolicyRecordFactory.FromDocument(documentJson);
        if (await factory.Policies.GetByPartitionKeyAsync(record.PartitionKey) is not null)
        {
            return;
        }

        _ = await factory.Policies.CreateAsync(record);
    }
}

public sealed class OrderingFixture : IDisposable
{
    internal OrderingCouponServiceFactory CouponFactory { get; } = new();

    internal OrderingOrderApiFactory OrderFactory { get; }

    public OrderingFixture()
    {
        var token = Auth.TestTokenFactory.CreateToken(CouponService.Api.Authentication.AuthorizationPolicies.Redeem);
        _ = CouponFactory.CreateClient();
        OrderFactory = new OrderingOrderApiFactory(CouponFactory, token);
    }

    internal HttpClient CreateOrderClient() => OrderFactory.CreateClient();

    public void Dispose()
    {
        OrderFactory.Dispose();
        CouponFactory.Dispose();
    }
}

internal static class OrderingRequests
{
    internal static object Checkout(
        string code,
        decimal clientTotal,
        object? lines = null,
        CouponCheckoutPolicy couponPolicy = CouponCheckoutPolicy.AllowWithoutDiscount) =>
        new
        {
            customerId = "customer-order",
            couponCode = code,
            clientTotal,
            couponPolicy,
            lines = lines ?? StandardBasket(),
        };

    internal static object StandardBasket() =>
        new[]
        {
            new { pizzaId = "margherita", quantity = 2 },
            new { pizzaId = "bbq-chicken", quantity = 1 },
        };

    internal static object SingleLineBasket() =>
        new[]
        {
            new { pizzaId = "margherita", quantity = 2 },
        };
}

internal sealed record PizzaCatalogResponse(
    string Currency,
    IReadOnlyList<PizzaResponse> Pizzas);

internal sealed record PizzaResponse(
    string Id,
    string Name,
    decimal UnitPrice,
    bool Vegetarian);

internal sealed record OrderResponse(
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

internal sealed record OrderLineResponse(
    string LineId,
    string PizzaId,
    string Category,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
