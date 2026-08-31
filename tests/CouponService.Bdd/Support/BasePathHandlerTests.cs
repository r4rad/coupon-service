using Xunit;

namespace CouponService.Bdd.Support;

public sealed class BasePathHandlerTests
{
    [Theory]
    // A gateway prefix must survive a root-relative request URI, or every call 404s.
    [InlineData("https://gw.azure-api.net/coupons/", "/v1/health/live", "https://gw.azure-api.net/coupons/v1/health/live")]
    [InlineData("https://gw.azure-api.net/coupons", "/v1/coupons/preview", "https://gw.azure-api.net/coupons/v1/coupons/preview")]
    [InlineData("https://gw.azure-api.net/admin/", "/v1/admin/policies", "https://gw.azure-api.net/admin/v1/admin/policies")]
    // A base with no path of its own leaves the request untouched.
    [InlineData("https://ca-coupon-api.azurecontainerapps.io/", "/v1/health/live", "https://ca-coupon-api.azurecontainerapps.io/v1/health/live")]
    public async Task Prefixes_the_base_path_onto_root_relative_requests(
        string baseUrl,
        string requestPath,
        string expected)
    {
        var sent = await SendAsync(baseUrl, requestPath);

        Assert.Equal(expected, sent);
    }

    [Fact]
    public async Task Leaves_a_request_that_already_carries_the_prefix_alone()
    {
        var sent = await SendAsync("https://gw.azure-api.net/coupons/", "/coupons/v1/health/live");

        Assert.Equal("https://gw.azure-api.net/coupons/v1/health/live", sent);
    }

    [Fact]
    public async Task Preserves_the_query_string()
    {
        var sent = await SendAsync("https://gw.azure-api.net/coupons/", "/v1/admin/policies?status=Active");

        Assert.Equal("https://gw.azure-api.net/coupons/v1/admin/policies?status=Active", sent);
    }

    private static async Task<string> SendAsync(string baseUrl, string requestPath)
    {
        var recorder = new UriRecordingHandler();
        var baseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);

        using var client = new HttpClient(new BasePathHandler(baseAddress, recorder))
        {
            BaseAddress = baseAddress,
        };

        using var response = await client.GetAsync(requestPath);

        return recorder.Observed?.ToString()
            ?? throw new InvalidOperationException("The handler chain never issued a request.");
    }

    private sealed class UriRecordingHandler : HttpMessageHandler
    {
        internal Uri? Observed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Observed = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
