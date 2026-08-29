using System.Net.Http;
using System.Net.Sockets;

namespace CouponService.IntegrationTests;

/// <summary>
/// Fast reachability probe used at discovery time. Must not hang: short TCP timeout only.
/// Full account handshake happens in <see cref="CosmosEmulatorFixture"/>.
/// </summary>
internal static class CosmosEmulatorGate
{
    internal const string EndpointHost = "127.0.0.1";
    internal const int EndpointPort = 8081;

    internal const string ConnectionString =
        "AccountEndpoint=https://localhost:8081/;"
        + "AccountKey=C2y6y7l0Mh4REIkobijaOXPUXaddrtqksydEIs3DOReuFs5Spre5VbttG1HbQdr9JwYEgETuNXvXvKJ1hVicPg==";

    internal const string SkipReason =
        "Cosmos emulator is not reachable at https://localhost:8081/. "
        + "Start it with `docker compose up -d` (first boot typically 30–60s; see docker-compose.yml for the certificate step), "
        + "then re-run: dotnet test tests/CouponService.IntegrationTests";

    private static readonly Lazy<bool> Reachable = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static bool IsReachable => Reachable.Value;

    private static bool Probe()
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(EndpointHost, EndpointPort);
            if (!connect.Wait(TimeSpan.FromSeconds(2)))
            {
                return false;
            }

            return client.Connected;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (AggregateException ex) when (ex.InnerException is SocketException)
        {
            return false;
        }
    }

    internal static HttpClient CreateEmulatorHttpClient() =>
        new(new HttpClientHandler
        {
            // Emulator presents a self-signed cert; documented in docker-compose.yml.
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
}
