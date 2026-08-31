namespace CouponService.Bdd.Support;

/// <summary>
/// Restores the base address's path segment on outbound requests.
/// </summary>
/// <remarks>
/// The step definitions address endpoints as <c>/v1/...</c>, and <see cref="HttpClient"/> resolves
/// a root-relative request URI against the authority alone, discarding whatever path a gateway
/// publishes the API under. Against APIM, which serves the Coupon Service at <c>/coupons</c>, every
/// call would otherwise arrive at a path with no API behind it and come back 404.
/// </remarks>
internal sealed class BasePathHandler : DelegatingHandler
{
    private readonly string _prefix;

    internal BasePathHandler(Uri baseAddress, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _prefix = baseAddress.AbsolutePath.TrimEnd('/');
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;

        if (_prefix.Length > 0
            && uri is not null
            && !uri.AbsolutePath.StartsWith(_prefix + "/", StringComparison.Ordinal))
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Path = _prefix + uri.AbsolutePath,
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
