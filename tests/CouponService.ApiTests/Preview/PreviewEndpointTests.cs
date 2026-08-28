using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CouponService.Api.Contracts.Preview;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;

namespace CouponService.ApiTests.Preview;

public sealed class PreviewEndpointTests : IClassFixture<CouponApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CouponApiFactory _factory;

    public PreviewEndpointTests(CouponApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rejected_coupon_returns_200_with_reason_and_full_price_breakdown()
    {
        await SeedPolicyAsync(PreviewTestDocuments.MinimumOrderDocument);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            PreviewTestRequests.MinimumNotMet());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadPreviewResponseAsync(response);
        Assert.Equal(CouponStatus.Rejected, body.Status);
        Assert.Equal(RejectionReason.MinimumOrderNotMet, body.Reason);
        Assert.Equal(21.90m, body.Pricing.Subtotal);
        Assert.Equal(0m, body.Pricing.Discount);
        Assert.Equal(21.90m, body.Pricing.Total);
    }

    [Fact]
    public async Task Rejected_coupon_includes_near_miss_hint_with_shortfall()
    {
        await SeedPolicyAsync(PreviewTestDocuments.MinimumOrderDocument);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            PreviewTestRequests.MinimumNotMet());

        var body = await ReadPreviewResponseAsync(response);

        Assert.NotNull(body.Hint);
        Assert.Equal(3.10m, body.Hint!.Shortfall);
        Assert.Contains("3.10", body.Hint.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expired_coupon_returns_200_with_reason_expired()
    {
        await SeedPolicyAsync(PreviewTestDocuments.ExpiredDocument);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            PreviewTestRequests.Expired());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadPreviewResponseAsync(response);
        Assert.Equal(CouponStatus.Rejected, body.Status);
        Assert.Equal(RejectionReason.Expired, body.Reason);
        Assert.Equal(31.00m, body.Pricing.Total);
    }

    [Fact]
    public async Task Malformed_body_returns_400_with_field_errors_and_correlation_id()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-400");

        var response = await client.PostAsJsonAsync(
            "/v1/coupons/preview",
            new
            {
                code = "",
                customerId = "customer-1",
                cart = new { lines = Array.Empty<object>() },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("correlationId", out var correlationId));
        Assert.Equal("test-correlation-400", correlationId.GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Preview_performs_no_repository_writes()
    {
        await SeedPolicyAsync(PreviewTestDocuments.Save10Document);

        var policyWritesBefore = _factory.Policies.WriteCount;
        var redemptionWritesBefore = _factory.Redemptions.WriteCount;

        using var client = _factory.CreateClient();
        _ = await client.PostAsJsonAsync("/v1/coupons/preview", PreviewTestRequests.Standard());

        Assert.Equal(policyWritesBefore, _factory.Policies.WriteCount);
        Assert.Equal(redemptionWritesBefore, _factory.Redemptions.WriteCount);
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

    private static async Task<PreviewResponse> ReadPreviewResponseAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<PreviewResponse>(JsonOptions);
        return body ?? throw new InvalidOperationException("Preview response body was empty.");
    }
}
