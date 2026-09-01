using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OrderApi.Options;
using OrderApi.Services;

namespace OrderApi.Auth;

public interface ICouponServiceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class ConfigurationCouponServiceTokenProvider(IOptions<OrderApiOptions> options)
    : ICouponServiceTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = options.Value.CouponServiceToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "OrderApi:CouponServiceToken must be configured for calls to the Coupon Service.");
        }

        return Task.FromResult(token);
    }
}

/// <summary>
/// Acquires an Entra access token from the Container Apps / App Service identity endpoint
/// (or IMDS) using the user-assigned managed identity — no client secret (AC-7.7).
/// </summary>
public sealed class ManagedIdentityCouponServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<OrderApiOptions> options,
    IClock clock) : ICouponServiceTokenProvider
{
    public const string HttpClientName = "OrderApi.ManagedIdentity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        if (_cachedToken is not null && _expiresAt > now.AddMinutes(5))
        {
            return _cachedToken;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = clock.UtcNow;
            if (_cachedToken is not null && _expiresAt > now.AddMinutes(5))
            {
                return _cachedToken;
            }

            var tokenResponse = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            _cachedToken = tokenResponse.AccessToken;
            _expiresAt = ParseExpiresOn(tokenResponse.ExpiresOn, now);
            return _cachedToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IdentityTokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        // Container Apps / App Service identity endpoints accept `resource`, not `scope`.
        // For v2 APIs (requestedAccessTokenVersion = 2), append /.default so app roles appear.
        var resource = options.Value.CouponServiceScope;
        if (string.IsNullOrWhiteSpace(resource))
        {
            throw new InvalidOperationException(
                "OrderApi:CouponServiceScope must be configured when UseManagedIdentity is true.");
        }

        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var identityEndpoint = Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT");
        var identityHeader = Environment.GetEnvironmentVariable("IDENTITY_HEADER");

        using var request = string.IsNullOrWhiteSpace(identityEndpoint)
            || string.IsNullOrWhiteSpace(identityHeader)
            ? CreateImdsRequest(resource, clientId)
            : CreateAppServiceRequest(identityEndpoint, identityHeader, resource, clientId);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var body = await JsonSerializer.DeserializeAsync<IdentityTokenResponse>(
                stream,
                JsonOptions,
                cancellationToken)
            .ConfigureAwait(false);

        if (body is null || string.IsNullOrWhiteSpace(body.AccessToken))
        {
            throw new InvalidOperationException("Managed identity endpoint returned an empty access token.");
        }

        return body;
    }

    private static HttpRequestMessage CreateAppServiceRequest(
        string identityEndpoint,
        string identityHeader,
        string resource,
        string? clientId)
    {
        var uri = AppendQuery(
            identityEndpoint,
            ("api-version", "2019-08-01"),
            ("resource", resource),
            string.IsNullOrWhiteSpace(clientId) ? null : ("client_id", clientId));

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-IDENTITY-HEADER", identityHeader);
        return request;
    }

    private static HttpRequestMessage CreateImdsRequest(string resource, string? clientId)
    {
        var uri = AppendQuery(
            "http://169.254.169.254/metadata/identity/oauth2/token",
            ("api-version", "2018-02-01"),
            ("resource", resource),
            string.IsNullOrWhiteSpace(clientId) ? null : ("client_id", clientId));

        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Metadata", "true");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static Uri AppendQuery(string baseUrl, params (string Key, string Value)?[] pairs)
    {
        var builder = new UriBuilder(baseUrl);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(builder.Query))
        {
            parts.Add(builder.Query.TrimStart('?'));
        }

        foreach (var pair in pairs)
        {
            if (pair is null)
            {
                continue;
            }

            parts.Add($"{Uri.EscapeDataString(pair.Value.Key)}={Uri.EscapeDataString(pair.Value.Value)}");
        }

        builder.Query = string.Join("&", parts);
        return builder.Uri;
    }

    private static DateTimeOffset ParseExpiresOn(string? expiresOn, DateTimeOffset fallbackNow)
    {
        if (string.IsNullOrWhiteSpace(expiresOn))
        {
            return fallbackNow.AddMinutes(5);
        }

        if (long.TryParse(expiresOn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (DateTimeOffset.TryParse(expiresOn, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return fallbackNow.AddMinutes(5);
    }

    private sealed record IdentityTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_on")] string? ExpiresOn);
}
