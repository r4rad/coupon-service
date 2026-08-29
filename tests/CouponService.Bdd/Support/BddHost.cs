using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CouponService.Bdd.Support;

/// <summary>
/// Shared HTTP clients and run-scoped policy lifecycle for the BDD suite (AC-10.4).
/// </summary>
public sealed class BddHost : IAsyncDisposable
{
    private readonly BddOptions _options;
    private readonly TokenProvider _tokens;
    private readonly List<string> _seededPolicyIds = new();
    private CouponServiceFactory? _couponFactory;
    private OrderApiFactory? _orderFactory;
    private HttpClient? _couponClient;
    private HttpClient? _orderClient;
    private HttpClient? _adminClient;
    private bool _started;

    internal BddHost(BddOptions options)
    {
        _options = options;
        _tokens = new TokenProvider(options);
        Clock = new MutableClock(new DateTimeOffset(2026, 8, 28, 15, 0, 0, TimeSpan.Zero));
        RunPrefix = $"RUN{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}_";
    }

    internal MutableClock Clock { get; }

    /// <summary>Per-run policy code prefix, e.g. RUN7F3A_.</summary>
    public string RunPrefix { get; }

    public static BddHost CreateFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables(prefix: "BDD_")
            .Build();

        var options = configuration.GetSection(BddOptions.SectionName).Get<BddOptions>()
            ?? new BddOptions();

        return new BddHost(options);
    }

    public async Task EnsureStartedAsync()
    {
        if (_started)
        {
            return;
        }

        if (_options.IsInProcess)
        {
            _couponFactory = new CouponServiceFactory(_tokens, Clock);
            _ = _couponFactory.Server;

            _orderFactory = new OrderApiFactory(_couponFactory, _tokens.CreateRedeemToken());
            _ = _orderFactory.Server;

            _couponClient = _couponFactory.CreateClient();
            _orderClient = _orderFactory.CreateClient();
            _adminClient = _couponFactory.CreateClient();
            _adminClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokens.CreateAdminToken());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_options.CouponServiceBaseUrl)
                || string.IsNullOrWhiteSpace(_options.OrderApiBaseUrl))
            {
                throw new InvalidOperationException(
                    "Bdd:Mode is Http but CouponServiceBaseUrl / OrderApiBaseUrl are missing. " +
                    "Set them in appsettings or BDD_ environment variables.");
            }

            _couponClient = CreateExternalClient(_options.CouponServiceBaseUrl);
            _orderClient = CreateExternalClient(_options.OrderApiBaseUrl);
            _adminClient = CreateExternalClient(_options.CouponServiceBaseUrl);
            _adminClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokens.CreateAdminToken());
        }

        await ProbeReachabilityAsync().ConfigureAwait(false);
        _started = true;
    }

    public HttpClient CouponAnonymous => RequireClient(_couponClient);

    public HttpClient Order => RequireClient(_orderClient);

    public HttpClient Admin => RequireClient(_adminClient);

    public HttpClient CreateRedeemClient()
    {
        var client = CloneCouponClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateRedeemToken());
        return client;
    }

    public HttpClient CreateCustomerClient()
    {
        var client = CloneCouponClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _tokens.CreateCustomerToken());
        return client;
    }

    public string Prefixed(string logicalCode) => RunPrefix + logicalCode;

    public async Task<string> SeedPolicyAsync(string documentJson)
    {
        await EnsureStartedAsync().ConfigureAwait(false);

        using var content = new StringContent(documentJson, Encoding.UTF8, "application/json");
        var response = await Admin.PostAsync("/v1/admin/policies", content).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Failed to seed policy via admin API ({(int)response.StatusCode}): {body}");
        }

        using var json = JsonDocument.Parse(body);
        var policyId = json.RootElement.GetProperty("policyId").GetString()
            ?? throw new InvalidOperationException("Admin create response lacked policyId.");

        _seededPolicyIds.Add(policyId);

        // Fixed/mutable clocks do not advance on their own; bust AutomaticPolicyIndex TTL (60s).
        Clock.Advance(TimeSpan.FromSeconds(61));

        return policyId;
    }

    public async Task TeardownSeededPoliciesAsync()
    {
        if (_adminClient is null || _seededPolicyIds.Count == 0)
        {
            _seededPolicyIds.Clear();
            return;
        }

        foreach (var policyId in _seededPolicyIds.ToArray())
        {
            try
            {
                await Admin.DeleteAsync($"/v1/admin/policies/{Uri.EscapeDataString(policyId)}")
                    .ConfigureAwait(false);
            }
            catch
            {
                // Best-effort teardown; next run uses a new prefix.
            }
        }

        _seededPolicyIds.Clear();
        Clock.Advance(TimeSpan.FromSeconds(61));
    }

    public async ValueTask DisposeAsync()
    {
        await TeardownSeededPoliciesAsync().ConfigureAwait(false);

        _adminClient?.Dispose();
        _couponClient?.Dispose();
        _orderClient?.Dispose();

        if (_orderFactory is not null)
        {
            await _orderFactory.DisposeAsync().ConfigureAwait(false);
        }

        if (_couponFactory is not null)
        {
            await _couponFactory.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task ProbeReachabilityAsync()
    {
        try
        {
            using var response = await CouponAnonymous
                .GetAsync("/v1/health/live")
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Coupon Service at '{CouponAnonymous.BaseAddress}' returned {(int)response.StatusCode} " +
                    "for /v1/health/live. The BDD target is unreachable or not ready.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Coupon Service BDD target is unreachable ({CouponAnonymous.BaseAddress}). " +
                $"Check Bdd:Mode / Bdd:CouponServiceBaseUrl. Underlying error: {ex.Message}",
                ex);
        }
    }

    private HttpClient CloneCouponClient()
    {
        if (_couponFactory is not null)
        {
            return _couponFactory.CreateClient();
        }

        return CreateExternalClient(_options.CouponServiceBaseUrl);
    }

    private static HttpClient CreateExternalClient(string baseUrl)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        return client;
    }

    private static HttpClient RequireClient(HttpClient? client) =>
        client ?? throw new InvalidOperationException("BDD host has not been started.");
}
