using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CouponService.Api.Contracts.Preview;
using CouponService.Api.Controllers.V1;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;

namespace CouponService.ApiTests.Contract;

public sealed class ContractStatusMatrixTests : IClassFixture<ContractApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ContractApiFactory _factory;

    public ContractStatusMatrixTests(ContractApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preview_applied_returns_200_with_discount_breakdown()
    {
        await SeedPolicyAsync(ContractTestData.Save10Document("SAVE10-CONTRACT", "save10-contract"));

        using var client = _factory.CreateAnonymousClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/preview")
            {
                Content = JsonContent.Create(ContractTestData.PreviewAppliedRequest()),
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ContractProblemDetailsAssertions.AssertCorrelationHeader(response);

        var body = await response.Content.ReadFromJsonAsync<PreviewResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(CouponStatus.Applied, body!.Status);
        Assert.Null(body.Reason);
        Assert.Equal(31.00m, body.Pricing.Subtotal);
        Assert.Equal(3.10m, body.Pricing.Discount);
        Assert.Equal(27.90m, body.Pricing.Total);
    }

    [Fact]
    public async Task Preview_rejected_returns_200_with_reason_and_full_price_breakdown()
    {
        await SeedPolicyAsync(ContractTestData.MinimumOrderDocument("MIN25-CONTRACT", "min25-contract"));

        using var client = _factory.CreateAnonymousClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/preview")
            {
                Content = JsonContent.Create(ContractTestData.PreviewRejectedRequest()),
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ContractProblemDetailsAssertions.AssertCorrelationHeader(response);

        var body = await response.Content.ReadFromJsonAsync<PreviewResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(CouponStatus.Rejected, body!.Status);
        Assert.Equal(RejectionReason.MinimumOrderNotMet, body.Reason);
        Assert.Equal(19.00m, body.Pricing.Subtotal);
        Assert.Equal(0m, body.Pricing.Discount);
        Assert.Equal(19.00m, body.Pricing.Total);
    }

    [Fact]
    public async Task Malformed_preview_returns_400_with_problem_details_shape_and_correlation_id()
    {
        using var client = _factory.CreateAnonymousClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/preview")
            {
                Content = JsonContent.Create(ContractTestData.MalformedPreviewRequest()),
            });

        var response = await client.SendAsync(request);

        await ContractProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.BadRequest,
            expectFieldErrors: true);
    }

    [Fact]
    public async Task Reservation_without_token_returns_401_with_correlation_id()
    {
        using var client = _factory.CreateAnonymousClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/reservations")
            {
                Content = JsonContent.Create(
                    ContractTestData.ReserveRequest("SAVE10-CONTRACT", "order-no-token-contract")),
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ContractProblemDetailsAssertions.AssertCorrelationHeader(response);
    }

    [Fact]
    public async Task Reservation_with_wrong_role_returns_403_with_correlation_id()
    {
        using var client = _factory.CreateAdminClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/reservations")
            {
                Content = JsonContent.Create(
                    ContractTestData.ReserveRequest("SAVE10-CONTRACT", "order-wrong-role-contract")),
            });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ContractProblemDetailsAssertions.AssertCorrelationHeader(response);
    }

    [Fact]
    public async Task Confirm_unknown_reservation_returns_404_with_problem_details()
    {
        using var client = _factory.CreateRedeemClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/reservations/unknown-order-contract/confirm"));

        var response = await client.SendAsync(request);

        await ContractProblemDetailsAssertions.AssertProblemDetailsAsync(
            response,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reserve_when_cap_consumed_returns_409_with_usage_limit_reason()
    {
        const string code = "LIMITED-CONTRACT";
        await SeedPolicyAsync(ContractTestData.LimitedOneUseDocument(code, "limited-contract"));

        using var firstClient = _factory.CreateRedeemClient();
        var first = await firstClient.PostAsJsonAsync(
            "/v1/reservations",
            ContractTestData.ReserveRequest(code, "order-cap-first-contract"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var secondClient = _factory.CreateRedeemClient();
        using var request = ContractProblemDetailsAssertions.WithCorrelationId(
            new HttpRequestMessage(HttpMethod.Post, "/v1/reservations")
            {
                Content = JsonContent.Create(
                    ContractTestData.ReserveRequest(code, "order-cap-second-contract")),
            });

        var response = await secondClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ContractProblemDetailsAssertions.AssertCorrelationHeader(response);

        var body = await response.Content.ReadFromJsonAsync<ReservationConflictResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(RejectionReason.UsageLimitReached, body!.Reason);
    }

    [Fact]
    public async Task Admin_update_with_stale_etag_returns_412_with_problem_details()
    {
        using var client = _factory.CreateAdminClient();
        const string policyId = "etag-contract";
        const string code = "ETAGCONTRACT";

        var created = await CreatePolicyAsync(client, ContractTestData.AdminDraftDocument(policyId, code));
        var staleEtag = created.ETag;

        using var firstUpdate = ContractProblemDetailsAssertions.WithCorrelationId(
            CreatePutRequest(policyId, ContractTestData.AdminDraftDocument(policyId, code), created.ETag));
        var firstResponse = await client.SendAsync(firstUpdate);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        using var staleUpdate = ContractProblemDetailsAssertions.WithCorrelationId(
            CreatePutRequest(policyId, ContractTestData.AdminDraftDocument(policyId, code), staleEtag));
        var staleResponse = await client.SendAsync(staleUpdate);

        await ContractProblemDetailsAssertions.AssertProblemDetailsAsync(
            staleResponse,
            HttpStatusCode.PreconditionFailed);
    }

    private async Task<AdminPolicyResponse> CreatePolicyAsync(HttpClient client, string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        var response = await client.PostAsJsonAsync("/v1/admin/policies", document.RootElement.Clone());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AdminPolicyResponse>(JsonOptions);
        return body ?? throw new InvalidOperationException("Policy response body was empty.");
    }

    private static HttpRequestMessage CreatePutRequest(string policyId, string documentJson, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/admin/policies/{policyId}")
        {
            Content = new StringContent(documentJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
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
