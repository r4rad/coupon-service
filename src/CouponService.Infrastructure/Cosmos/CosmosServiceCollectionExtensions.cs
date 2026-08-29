using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CouponService.Infrastructure.Cosmos;

public static class CosmosServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="CosmosClient"/> with source-generated System.Text.Json serialization,
    /// plus policy, redemption and order repositories against the three containers (design § Cosmos containers).
    /// </summary>
    public static IServiceCollection AddCouponCosmosRepositories(
        this IServiceCollection services,
        CosmosOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton(_ => CreateClient(options));

        services.AddSingleton<Application.Policies.IPolicyRepository>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var container = client.GetContainer(options.DatabaseName, options.PoliciesContainerName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Cosmos.Policies");
            return new CosmosPolicyRepository(new CosmosItemStore(container, logger));
        });

        services.AddSingleton<Application.Redemption.IRedemptionRepository>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var container = client.GetContainer(options.DatabaseName, options.RedemptionsContainerName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var storeLogger = loggerFactory.CreateLogger("Cosmos.Redemptions");
            return new CosmosRedemptionRepository(
                new CosmosItemStore(container, storeLogger),
                loggerFactory.CreateLogger<CosmosRedemptionRepository>());
        });

        services.AddSingleton<ICosmosOrderRepository>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var container = client.GetContainer(options.DatabaseName, options.OrdersContainerName);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Cosmos.Orders");
            return new CosmosOrderRepository(new CosmosItemStore(container, logger));
        });

        return services;
    }

    internal static CosmosClient CreateClient(CosmosOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        var serializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                CosmosJsonContext.Default,
                new DefaultJsonTypeInfoResolver()),
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        // Singleton client — constructing per request collapses throughput (CS-23).
        return new CosmosClient(
            options.ConnectionString,
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = serializerOptions,
                AllowBulkExecution = false,
            });
    }
}
