using System.Net;
using System.Net.Http.Json;
using CouponService.Api.Observability;
using CouponService.Application.Policies;
using CouponService.Infrastructure.Logging;
using CouponService.ApiTests.Preview;
using CouponService.ApiTests.Reservations;
using Microsoft.AspNetCore.Http;

namespace CouponService.ApiTests.Observability;

public sealed class StructuredLoggingTests : IClassFixture<ObservabilityApiFactory>
{
    private const string CallerCorrelationId = "corr-supplied-by-caller-00112233445566778899aabbccddeeff";

    private readonly ObservabilityApiFactory _factory;

    public StructuredLoggingTests(ObservabilityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_log_contains_supplied_correlation_id_outcome_and_duration()
    {
        _factory.LogSink.Clear();
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document("OBS-SAVE10", "obs-save10"));

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/preview")
        {
            Content = JsonContent.Create(ObservabilityTestRequests.Preview("OBS-SAVE10")),
        };
        request.Headers.TryAddWithoutValidation(CorrelationContext.CorrelationIdHeaderName, CallerCorrelationId);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CallerCorrelationId, response.Headers.GetValues(CorrelationContext.CorrelationIdHeaderName).Single());

        var lines = _factory.LogSink.Lines;
        Assert.Contains(lines, line => line.Contains(CallerCorrelationId, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("\"Outcome\"", StringComparison.Ordinal) && line.Contains("Success", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("\"DurationMs\"", StringComparison.Ordinal));
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
}

public sealed class CorrelationPropagationTests
{
    [Fact]
    public async Task Outgoing_http_client_receives_traceparent_and_correlation_id()
    {
        var context = new DefaultHttpContext();
        const string correlationId = "abc123def4567890abc123def4567890";
        var traceParent = TraceParent.Create(correlationId);
        context.Items[CorrelationContext.CorrelationIdItemKey] = correlationId;
        context.Items[CorrelationContext.TraceParentItemKey] = traceParent;

        var accessor = new HttpContextAccessor { HttpContext = context };
        var handler = new CorrelationHttpMessageHandler(accessor)
        {
            InnerHandler = new RecordingHandler(),
        };

        using var client = new HttpClient(handler);
        _ = await client.GetAsync("https://example.test/internal");

        Assert.Equal(traceParent, RecordingHandler.LastRequest?.Headers.GetValues(TraceParent.HeaderName).Single());
        Assert.Equal(
            correlationId,
            RecordingHandler.LastRequest?.Headers.GetValues(CorrelationContext.CorrelationIdHeaderName).Single());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        internal static HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

public sealed class DomainEventLoggingTests : IClassFixture<ObservabilityApiFactory>
{
    private readonly ObservabilityApiFactory _factory;

    public DomainEventLoggingTests(ObservabilityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preview_emits_coupon_previewed_and_applied_domain_events()
    {
        _factory.LogSink.Clear();
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document("OBS-APPLY", "obs-apply"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            ObservabilityTestRequests.Preview("OBS-APPLY"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lines = string.Join('\n', _factory.LogSink.Lines);
        Assert.Contains(DomainEventNames.CouponPreviewed, lines, StringComparison.Ordinal);
        Assert.Contains(DomainEventNames.CouponApplied, lines, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_emits_coupon_rejected_for_ineligible_basket()
    {
        _factory.LogSink.Clear();
        await SeedPolicyAsync(PreviewTestDocuments.MinimumOrderDocument);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            ObservabilityTestRequests.Preview("MIN25", subtotalLines: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lines = string.Join('\n', _factory.LogSink.Lines);
        Assert.Contains(DomainEventNames.CouponPreviewed, lines, StringComparison.Ordinal);
        Assert.Contains(DomainEventNames.CouponRejected, lines, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reserve_emits_reservation_created_domain_event()
    {
        _factory.LogSink.Clear();
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document("OBS-RESERVE", "obs-reserve"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/reservations",
            ReservationTestRequests.Reserve("OBS-RESERVE", "order-obs-reserve"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var lines = string.Join('\n', _factory.LogSink.Lines);
        Assert.Contains(DomainEventNames.ReservationCreated, lines, StringComparison.Ordinal);
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
}

public sealed class SensitiveDataRedactionTests : IClassFixture<ObservabilityApiFactory>
{
    private const string BearerSecret = "super-secret-bearer-token-value";

    private const string ConnectionSecret = "AccountEndpoint=https://example.documents.azure.com:443/;AccountKey=super-secret-key;";

    private const string CustomerEmail = "customer.sensitive@example.com";

    private readonly ObservabilityApiFactory _factory;

    public SensitiveDataRedactionTests(ObservabilityApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Logs_never_contain_bearer_token_connection_string_or_customer_email()
    {
        _factory.LogSink.Clear();
        await SeedPolicyAsync(ReservationTestDocuments.Save10Document("OBS-REDACT", "obs-redact"));

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/preview")
        {
            Content = JsonContent.Create(ObservabilityTestRequests.Preview("OBS-REDACT")),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BearerSecret);
        request.Headers.TryAddWithoutValidation("X-Customer-Email", CustomerEmail);
        request.Headers.TryAddWithoutValidation("X-Connection-String", ConnectionSecret);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var combined = string.Join('\n', _factory.LogSink.Lines);
        Assert.DoesNotContain(BearerSecret, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(ConnectionSecret, combined, StringComparison.Ordinal);
        Assert.DoesNotContain(CustomerEmail, combined, StringComparison.Ordinal);
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
}

internal static class ObservabilityTestRequests
{
    internal static object Preview(string code, int subtotalLines = 2) =>
        new
        {
            code,
            customerId = "customer-obs",
            confirmedOrderCount = 0,
            cart = new
            {
                lines = Enumerable.Range(1, subtotalLines).Select(index => new
                {
                    lineId = $"line-{index}",
                    pizzaId = "margherita",
                    category = "classic",
                    unitPrice = index == 1 ? 9.50m : 12.00m,
                    quantity = 2,
                }).ToArray(),
            },
        };
}
