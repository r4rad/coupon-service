using System.Text.Json;

namespace CouponService.Bdd.Support;

/// <summary>Per-scenario scratch state shared across step definitions.</summary>
public sealed class ScenarioState
{
    /// <summary>
    /// Unique prefix per scenario so soft-archived policies (DELETE) cannot collide on partition key (AC-10.4).
    /// </summary>
    public string PolicyPrefix { get; } =
        $"RUN{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}_";

    public List<object> CartLines { get; } = new();

    public string? ActiveCode { get; set; }

    public string? LastPolicyId { get; set; }

    public HttpResponseMessage? LastResponse { get; set; }

    public JsonDocument? LastJson { get; set; }

    public List<HttpResponseMessage> ConcurrentResponses { get; } = new();

    public bool ServiceRedeployed { get; set; }

    public string? CustomerTokenMode { get; set; }

    public string Prefixed(string logicalCode) => PolicyPrefix + logicalCode;

    public void ReplaceJson(JsonDocument? document)
    {
        LastJson?.Dispose();
        LastJson = document;
    }
}
