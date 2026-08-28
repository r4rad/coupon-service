using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderApi.Auth;
using OrderApi.Options;

namespace OrderApi.Clients;

public sealed class HttpCouponServiceClient(
    HttpClient httpClient,
    ICouponServiceTokenProvider tokenProvider) : ICouponServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CouponReservationResult> ReserveAsync(
        CouponReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = await CreateRequestAsync(
                HttpMethod.Post,
                "/v1/reservations",
                new
                {
                    orderId = request.OrderId,
                    code = request.Code,
                    customerId = request.CustomerId,
                    confirmedOrderCount = request.ConfirmedOrderCount,
                    cart = new
                    {
                        lines = request.Lines.Select(line => new
                        {
                            lineId = line.LineId,
                            pizzaId = line.PizzaId,
                            category = line.Category,
                            unitPrice = line.UnitPrice,
                            quantity = line.Quantity,
                        }).ToArray(),
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new CouponReservationResult(false, null, null, Unreachable: true);
        }
        catch (TaskCanceledException)
        {
            return new CouponReservationResult(false, null, null, Unreachable: true);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Created)
            {
                var body = await response.Content
                    .ReadFromJsonAsync<ReservationCreatedResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                return new CouponReservationResult(
                    true,
                    body?.Pricing is null
                        ? null
                        : new CouponPricing(
                            body.Pricing.Currency,
                            body.Pricing.Subtotal,
                            body.Pricing.Discount,
                            body.Pricing.Total),
                    null,
                    false);
            }

            if (response.StatusCode is HttpStatusCode.Conflict)
            {
                var conflict = await response.Content
                    .ReadFromJsonAsync<ReservationConflictResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);

                return new CouponReservationResult(
                    false,
                    null,
                    conflict?.Reason.ToString(),
                    false);
            }

            response.EnsureSuccessStatusCode();
            return new CouponReservationResult(false, null, "UnexpectedResponse", false);
        }
    }

    public async Task ConfirmAsync(string orderId, CancellationToken cancellationToken = default)
    {
        using var message = await CreateRequestAsync(
                HttpMethod.Post,
                $"/v1/reservations/{orderId}/confirm",
                content: null,
                cancellationToken)
            .ConfigureAwait(false);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReleaseAsync(string orderId, string reason, CancellationToken cancellationToken = default)
    {
        using var message = await CreateRequestAsync(
                HttpMethod.Post,
                $"/v1/reservations/{orderId}/release",
                new { reason },
                cancellationToken)
            .ConfigureAwait(false);

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        object? content,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }

    private sealed record ReservationCreatedResponse(PricingResponse Pricing);

    private sealed record PricingResponse(
        string Currency,
        decimal Subtotal,
        decimal Discount,
        decimal Total);

    private sealed record ReservationConflictResponse(RejectionReason Reason);

    private enum RejectionReason
    {
        NotFound,
        Expired,
        NotYetActive,
        MinimumOrderNotMet,
        CategoryNotEligible,
        UsageLimitReached,
        PerCustomerLimitReached,
        NotFirstOrder,
        DayNotEligible,
        Disabled,
    }
}
