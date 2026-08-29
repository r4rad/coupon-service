using Microsoft.Extensions.Logging;

namespace CouponService.Infrastructure.Cosmos;

/// <summary>AC-8.5: every Cosmos operation logs RequestCharge as a structured field.</summary>
internal static class CosmosRequestChargeLogging
{
    internal const string RequestChargePropertyName = "RequestCharge";

    private static readonly Action<ILogger, string, string, double, Exception?> LogAction =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Information,
            new EventId(8501, "CosmosRequestCharge"),
            "Cosmos {CosmosOperation} on {CosmosContainer} completed with RequestCharge {RequestCharge}");

    internal static void Log(
        ILogger logger,
        string operation,
        string container,
        double requestCharge) =>
        LogAction(logger, operation, container, requestCharge, null);
}
