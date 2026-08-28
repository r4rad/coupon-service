using System.Net;
using System.Text.Json;
using CouponService.Api.Observability;

namespace CouponService.ApiTests.Contract;

internal static class ContractProblemDetailsAssertions
{
    internal const string CorrelationId = "contract-test-correlation-id";

    internal static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        bool expectFieldErrors = false)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        AssertCorrelationHeader(response);

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));

        using var problem = JsonDocument.Parse(body);
        var root = problem.RootElement;

        Assert.True(root.TryGetProperty("type", out var type));
        Assert.False(string.IsNullOrWhiteSpace(type.GetString()));
        Assert.True(root.TryGetProperty("title", out var title));
        Assert.False(string.IsNullOrWhiteSpace(title.GetString()));
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("correlationId", out var correlationId));
        Assert.Equal(CorrelationId, correlationId.GetString());

        if (expectFieldErrors)
        {
            Assert.True(root.TryGetProperty("errors", out var errors));
            Assert.Equal(JsonValueKind.Object, errors.ValueKind);
            Assert.NotEmpty(errors.EnumerateObject());
        }
    }

    internal static void AssertCorrelationHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(CorrelationContext.CorrelationIdHeaderName, out var values));
        Assert.Equal(CorrelationId, values.Single());
    }

    internal static HttpRequestMessage WithCorrelationId(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation(CorrelationContext.CorrelationIdHeaderName, CorrelationId);
        return request;
    }
}
