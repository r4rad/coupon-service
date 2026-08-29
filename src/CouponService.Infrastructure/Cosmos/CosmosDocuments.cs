using System.Text.Json;
using System.Text.Json.Serialization;

namespace CouponService.Infrastructure.Cosmos;

public sealed class PolicyDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    /// <summary>Denormalised for filtered automatic-policy queries without parsing DocumentJson.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("documentJson")]
    public string DocumentJson { get; set; } = string.Empty;

    [JsonPropertyName("_etag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CosmosETag { get; set; }
}

public sealed class CounterDocument
{
    public const string DocumentId = "counter";

    public const string DocumentType = "counter";

    [JsonPropertyName("id")]
    public string Id { get; set; } = DocumentId;

    [JsonPropertyName("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = DocumentType;

    [JsonPropertyName("confirmedCount")]
    public int ConfirmedCount { get; set; }

    [JsonPropertyName("activeReservations")]
    public int ActiveReservations { get; set; }

    [JsonPropertyName("_etag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CosmosETag { get; set; }
}

public sealed class RedemptionDocument
{
    public const string DocumentType = "redemption";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = DocumentType;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("discountApplied")]
    public decimal DiscountApplied { get; set; }

    [JsonPropertyName("policyContentHash")]
    public string PolicyContentHash { get; set; } = string.Empty;

    /// <summary>Cosmos TTL in seconds while Reserved; omitted (null) when confirmed/released/expired.</summary>
    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; set; }

    [JsonPropertyName("ttlExpiresAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? TtlExpiresAt { get; set; }

    [JsonPropertyName("_etag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CosmosETag { get; set; }
}

public sealed class OrderDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("discount")]
    public decimal Discount { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("couponCode")]
    public string? CouponCode { get; set; }

    [JsonPropertyName("couponRejectionReason")]
    public string? CouponRejectionReason { get; set; }

    [JsonPropertyName("couponServiceDegraded")]
    public bool CouponServiceDegraded { get; set; }

    [JsonPropertyName("lines")]
    public IReadOnlyList<OrderLineDocument> Lines { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OrderLineDocument
{
    [JsonPropertyName("lineId")]
    public string LineId { get; set; } = string.Empty;

    [JsonPropertyName("pizzaId")]
    public string PizzaId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; set; }
}

[JsonSerializable(typeof(PolicyDocument))]
[JsonSerializable(typeof(CounterDocument))]
[JsonSerializable(typeof(RedemptionDocument))]
[JsonSerializable(typeof(OrderDocument))]
[JsonSerializable(typeof(OrderLineDocument))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class CosmosJsonContext : JsonSerializerContext;
