using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CouponService.Api.Controllers.V1;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;

namespace CouponService.ApiTests.Reservations;

public sealed class ReservationEndpointTests : IClassFixture<ReservationApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ReservationApiFactory _factory;

    public ReservationEndpointTests(ReservationApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reserve_returns_201_with_authoritative_price_breakdown_when_checkout_begins()
    {
        const string code = "SAVE10-RESERVE";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-reserve"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-reserve-ac41"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadCreatedResponseAsync(response);
        Assert.Equal("order-reserve-ac41", body.OrderId);
        Assert.Equal(31.00m, body.Pricing.Subtotal);
        Assert.Equal(3.10m, body.Pricing.Discount);
        Assert.Equal(27.90m, body.Pricing.Total);
        Assert.Equal("EUR", body.Pricing.Currency);
    }

    [Fact]
    public async Task Reserve_response_total_is_independent_of_client_supplied_total()
    {
        const string code = "SAVE10-CLIENT";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-client"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-client-total-ac41"));

        var body = await ReadCreatedResponseAsync(response);
        Assert.Equal(27.90m, body.Pricing.Total);
        Assert.NotEqual(999.99m, body.Pricing.Total);
    }

    [Fact]
    public async Task Repeated_reserve_for_same_orderId_returns_existing_reservation_without_extra_writes()
    {
        const string code = "SAVE10-IDEM";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-idem"));

        using var client = _factory.CreateClient();
        var request = ReservationTestRequests.Reserve(code, "order-idempotent-ac41", "customer-1");

        var first = await client.PostAsJsonAsync("/v1/reservations", request);
        var writesAfterFirst = _factory.Redemptions.WriteCount;

        var second = await client.PostAsJsonAsync("/v1/reservations", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await ReadCreatedResponseAsync(first);
        var secondBody = await ReadCreatedResponseAsync(second);
        Assert.Equal(firstBody.OrderId, secondBody.OrderId);
        Assert.Equal(firstBody.Pricing.Total, secondBody.Pricing.Total);
        Assert.Equal(writesAfterFirst, _factory.Redemptions.WriteCount);
    }

    [Fact]
    public async Task Concurrent_reservations_for_last_remaining_use_grant_one_201_and_one_409()
    {
        const string code = "LIMITED-RACE";
        await SeedPolicyAsync(ReservationTestDocuments.LimitedOneUseDocument(code, "limited-race"));

        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        var taskA = clientA.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.LimitedOneReserve(code, "order-a-race", "customer-a"));
        var taskB = clientB.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.LimitedOneReserve(code, "order-b-race", "customer-b"));

        var responses = await Task.WhenAll(taskA, taskB);

        var created = responses.Where(response => response.StatusCode == HttpStatusCode.Created).ToArray();
        var conflicts = responses.Where(response => response.StatusCode == HttpStatusCode.Conflict).ToArray();

        Assert.Single(created);
        Assert.Single(conflicts);

        var conflictBody = await conflicts[0].Content.ReadFromJsonAsync<ReservationConflictResponse>(JsonOptions);
        Assert.NotNull(conflictBody);
        Assert.Equal(RejectionReason.UsageLimitReached, conflictBody!.Reason);
    }

    [Fact]
    public async Task Confirm_commits_a_reserved_redemption()
    {
        const string code = "SAVE10-CONFIRM";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-confirm"));

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-confirm-ac41"));

        var response = await client.PostAsync("/v1/reservations/order-confirm-ac41/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadTransitionResponseAsync(response);
        Assert.Equal("order-confirm-ac41", body.OrderId);
        Assert.Equal(RedemptionState.Confirmed, body.State);

        var counter = await _factory.Redemptions.GetCounterAsync(code);
        Assert.Equal(1, counter!.ConfirmedCount);
        Assert.Equal(0, counter.ActiveReservations);
    }

    [Fact]
    public async Task Confirm_called_twice_is_idempotent()
    {
        const string code = "SAVE10-CONFIRM2";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-confirm2"));

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-confirm-twice-ac41"));

        var first = await client.PostAsync("/v1/reservations/order-confirm-twice-ac41/confirm", null);
        var writesAfterFirst = _factory.Redemptions.WriteCount;
        var second = await client.PostAsync("/v1/reservations/order-confirm-twice-ac41/confirm", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(writesAfterFirst, _factory.Redemptions.WriteCount);

        var body = await ReadTransitionResponseAsync(second);
        Assert.Equal(RedemptionState.Confirmed, body.State);
    }

    [Fact]
    public async Task Release_returns_the_reserved_use_when_order_fails()
    {
        const string code = "SAVE10-RELEASE";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-release"));

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-release-ac41"));

        var response = await client.PostAsJsonAsync(
            "/v1/reservations/order-release-ac41/release",
            new { reason = "payment-failed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadTransitionResponseAsync(response);
        Assert.Equal("order-release-ac41", body.OrderId);
        Assert.Equal(RedemptionState.Released, body.State);

        var counter = await _factory.Redemptions.GetCounterAsync(code);
        Assert.Equal(0, counter!.ConfirmedCount);
        Assert.Equal(0, counter.ActiveReservations);
    }

    [Fact]
    public async Task Release_called_twice_is_idempotent()
    {
        const string code = "SAVE10-RELEASE2";
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document(code, "save10-release2"));

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve(code, "order-release-twice-ac41"));

        var first = await client.PostAsJsonAsync(
            "/v1/reservations/order-release-twice-ac41/release",
            new { reason = "payment-failed" });
        var writesAfterFirst = _factory.Redemptions.WriteCount;
        var second = await client.PostAsJsonAsync(
            "/v1/reservations/order-release-twice-ac41/release",
            new { reason = "payment-failed" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(writesAfterFirst, _factory.Redemptions.WriteCount);

        var body = await ReadTransitionResponseAsync(second);
        Assert.Equal(RedemptionState.Released, body.State);
    }

    [Fact]
    public async Task Confirm_returns_404_for_unknown_order()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/v1/reservations/unknown-order/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Release_returns_404_for_unknown_order()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations/unknown-order/release",
            new { reason = "payment-failed" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task SeedPolicyAsync(string documentJson)
    {
        var record = PolicyRecordFactory.FromDocument(documentJson);
        if (await _factory.Policies.GetByPartitionKeyAsync(record.PartitionKey) is not null)
        {
            return;
        }

        _ = await _factory.Policies.CreateAsync(record);
    }

    private static async Task<ReservationCreatedResponse> ReadCreatedResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ReservationCreatedResponse>(JsonOptions);
        return body ?? throw new InvalidOperationException("Reservation response body was empty.");
    }

    private static async Task<ReservationTransitionResponse> ReadTransitionResponseAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ReservationTransitionResponse>(JsonOptions);
        return body ?? throw new InvalidOperationException("Transition response body was empty.");
    }
}
