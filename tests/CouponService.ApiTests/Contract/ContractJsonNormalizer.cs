using System.Text.Json;
using System.Text.Json.Nodes;

namespace CouponService.ApiTests.Contract;

internal static class ContractJsonNormalizer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    internal static string Normalize(string json)
    {
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("OpenAPI document was empty.");

        var normalized = NormalizeNode(node);
        return normalized.ToJsonString(WriteOptions);
    }

    private static JsonNode NormalizeNode(JsonNode node) =>
        node switch
        {
            JsonObject jsonObject => NormalizeObject(jsonObject),
            JsonArray jsonArray => NormalizeArray(jsonArray),
            _ => node.DeepClone(),
        };

    private static JsonObject NormalizeObject(JsonObject jsonObject)
    {
        var normalized = new JsonObject();
        foreach (var property in jsonObject.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            normalized[property.Key] = NormalizeNode(property.Value!);
        }

        return normalized;
    }

    private static JsonArray NormalizeArray(JsonArray jsonArray)
    {
        var normalized = new JsonArray();
        foreach (var item in jsonArray)
        {
            normalized.Add(item is null ? null : NormalizeNode(item));
        }

        return normalized;
    }
}
