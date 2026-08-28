using System.Text.Json;

namespace CouponService.Application.Policies;

public static class PolicyDocumentMetadata
{
    public static int ReadPriority(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (!document.RootElement.TryGetProperty("priority", out var priority)
            || !priority.TryGetInt32(out var parsed))
        {
            return 0;
        }

        return parsed;
    }

    public static bool ReadStackable(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        return document.RootElement.TryGetProperty("stackable", out var stackable)
            && stackable.ValueKind is JsonValueKind.True;
    }

    public static PolicyStatus ReadStatus(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (!document.RootElement.TryGetProperty("status", out var status))
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

    public static PolicyTrigger ReadTrigger(string documentJson)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (!document.RootElement.TryGetProperty("trigger", out var trigger))
        {
            return PolicyTrigger.Code;
        }

        return string.Equals(trigger.GetString(), "automatic", StringComparison.OrdinalIgnoreCase)
            ? PolicyTrigger.Automatic
            : PolicyTrigger.Code;
    }
}
