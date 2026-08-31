using CouponService.Api.Authentication;

namespace CouponService.ApiTests.Auth;

/// <summary>
/// Version 2 access tokens carry the resource application's client id in <c>aud</c>, never the
/// Application ID URI. Both must validate or every Entra caller is rejected (AC-7.6).
/// </summary>
public sealed class JwtAudienceTests
{
    private const string Uri = "api://coupon-service";
    private const string ClientId = "189703ee-da8c-4fa4-8c0d-a53f193283f4";

    [Fact]
    public void Both_the_application_id_uri_and_the_client_id_are_valid_audiences()
    {
        var options = new JwtBearerOptions { Audience = Uri, ClientId = ClientId };

        Assert.Equal([Uri, ClientId], options.ValidAudiences());
    }

    [Fact]
    public void An_unconfigured_client_id_leaves_only_the_application_id_uri()
    {
        var options = new JwtBearerOptions { Audience = Uri, ClientId = "   " };

        Assert.Equal([Uri], options.ValidAudiences());
    }

    [Fact]
    public void A_client_id_equal_to_the_audience_is_not_repeated()
    {
        // main.bicep substitutes the Application ID URI when the client id is unknown, so the
        // two values can legitimately arrive identical.
        var options = new JwtBearerOptions { Audience = Uri, ClientId = Uri };

        Assert.Equal([Uri], options.ValidAudiences());
    }
}
