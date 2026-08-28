using System.Text.Json;
using CouponService.Application.Policies;

namespace CouponService.Application.Engine;

internal static class PolicyDocumentReader
{
    internal static JsonElement ParseRoot(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        return document.RootElement.Clone();
    }

    internal static string GetEngineSchema(JsonElement root) =>
        root.TryGetProperty("engineSchema", out var schema)
            ? schema.GetString() ?? string.Empty
            : string.Empty;

    internal static PolicyStatus GetStatus(JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status))
        {
            return PolicyStatus.Draft;
        }

        return status.GetString() switch
        {
            "Shadow" => PolicyStatus.Shadow,
            "Active" => PolicyStatus.Active,
            "Paused" => PolicyStatus.Paused,
            "Archived" => PolicyStatus.Archived,
            _ => PolicyStatus.Draft,
        };
    }

    internal static bool TryGetWindow(
        JsonElement root,
        out DateTimeOffset? from,
        out DateTimeOffset? to)
    {
        from = null;
        to = null;

        if (!root.TryGetProperty("window", out var window) || window.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (window.TryGetProperty("from", out var fromElement))
        {
            from = ParseDateTimeOffset(fromElement);
        }

        if (window.TryGetProperty("to", out var toElement))
        {
            to = ParseDateTimeOffset(toElement);
        }

        return from is not null || to is not null;
    }

    internal static string GetConditionJson(JsonElement root)
    {
        if (!root.TryGetProperty("condition", out var condition))
        {
            throw new InvalidOperationException("Policy document is missing $.condition.");
        }

        return condition.GetRawText();
    }

    internal static JsonElement GetEffect(JsonElement root)
    {
        if (!root.TryGetProperty("effect", out var effect))
        {
            throw new InvalidOperationException("Policy document is missing $.effect.");
        }

        return effect.Clone();
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return element.TryGetDateTimeOffset(out var parsed)
            ? parsed
            : DateTimeOffset.Parse(element.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    }
}
