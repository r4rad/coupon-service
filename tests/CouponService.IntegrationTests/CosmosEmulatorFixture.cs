using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CouponService.Infrastructure.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

namespace CouponService.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class CosmosEmulatorCollection : ICollectionFixture<CosmosEmulatorFixture>
{
    public const string Name = "CosmosEmulator";
}

/// <summary>
/// Shared emulator client and containers. Provisions the three design containers once per collection.
/// </summary>
public sealed class CosmosEmulatorFixture : IAsyncLifetime
{
    private CosmosClient? _client;

    public CosmosRedemptionRepository Redemptions { get; private set; } = null!;

    public Container RedemptionsContainer { get; private set; } = null!;

    public ICosmosItemStore RedemptionsStore { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!CosmosEmulatorGate.IsReachable)
        {
            return;
        }

        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        _client = new CosmosClient(
            CosmosEmulatorGate.ConnectionString,
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                LimitToEndpoint = true,
                HttpClientFactory = CosmosEmulatorGate.CreateEmulatorHttpClient,
                UseSystemTextJsonSerializerWithOptions = serializerOptions,
                RequestTimeout = TimeSpan.FromSeconds(15),
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await _client.ReadAccountAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or CosmosException)
        {
            // Leave repositories unset; tests that ran discovery with TCP open but account not ready
            // will fail loudly rather than hang. Prefer waiting for /ready before `dotnet test`.
            throw new InvalidOperationException(
                "Cosmos emulator port is open but the account is not ready. "
                + "Wait for https://localhost:8080/ready (see docker-compose.yml), then re-run tests.",
                ex);
        }

        var options = new CosmosOptions();
        var database = await _client
            .CreateDatabaseIfNotExistsAsync(options.DatabaseName, cancellationToken: cts.Token)
            .ConfigureAwait(false);

        await database.Database
            .CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = options.PoliciesContainerName,
                    PartitionKeyPath = "/pk",
                },
                cancellationToken: cts.Token)
            .ConfigureAwait(false);

        await database.Database
            .CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = options.RedemptionsContainerName,
                    PartitionKeyPath = "/pk",
                    // -1 enables per-item TTL without a default expiry (design: TTL while Reserved).
                    DefaultTimeToLive = -1,
                    UniqueKeyPolicy = new UniqueKeyPolicy
                    {
                        UniqueKeys =
                        {
                            new UniqueKey { Paths = { "/orderId" } },
                        },
                    },
                },
                cancellationToken: cts.Token)
            .ConfigureAwait(false);

        await database.Database
            .CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = options.OrdersContainerName,
                    PartitionKeyPath = "/orderId",
                },
                cancellationToken: cts.Token)
            .ConfigureAwait(false);

        RedemptionsContainer = _client.GetContainer(options.DatabaseName, options.RedemptionsContainerName);
        RedemptionsStore = new CosmosItemStore(
            RedemptionsContainer,
            NullLogger.Instance);
        Redemptions = new CosmosRedemptionRepository(
            RedemptionsStore,
            NullLogger<CosmosRedemptionRepository>.Instance);
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }
}
