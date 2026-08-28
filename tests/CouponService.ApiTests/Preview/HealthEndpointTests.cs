using System.Net;

namespace CouponService.ApiTests.Preview;

public sealed class PreviewHealthEndpointTests : IClassFixture<CouponApiFactory>
{
    private readonly CouponApiFactory _factory;

    public PreviewHealthEndpointTests(CouponApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Live_probe_returns_ok()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_probe_returns_ok_when_policy_repository_is_reachable()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
