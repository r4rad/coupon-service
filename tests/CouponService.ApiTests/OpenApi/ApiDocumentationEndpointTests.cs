using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CouponService.ApiTests.OpenApi;

public sealed class ApiDocumentationEndpointTests : IClassFixture<Preview.CouponApiFactory>
{
    private readonly Preview.CouponApiFactory _factory;

    public ApiDocumentationEndpointTests(Preview.CouponApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_includes_exposed_coupon_and_reservation_routes()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(document.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/v1/coupons/preview", out _));
        Assert.True(paths.TryGetProperty("/v1/reservations", out _));
        Assert.True(paths.TryGetProperty("/v1/reservations/{orderId}/confirm", out _));
        Assert.True(paths.TryGetProperty("/v1/reservations/{orderId}/release", out _));
    }

    [Fact]
    public async Task Scalar_ui_is_served_at_dedicated_route()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/scalar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("scalar", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Redoc_ui_is_served_at_dedicated_route()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/redoc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("redoc", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);
    }
}
