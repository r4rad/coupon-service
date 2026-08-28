using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CouponService.Engine.Caching;

public static class PolicyContentHasher
{
    public static string ComputeHash(string policyDocumentJson)
    {
        var canonical = Canonicalize(policyDocumentJson);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Canonicalize(string policyDocumentJson)
    {
        using var document = JsonDocument.Parse(policyDocumentJson);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        WriteCanonical(document.RootElement, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                WriteCanonicalNumber(element, writer);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException($"Unsupported JSON token '{element.ValueKind}'.");
        }
    }

    private static void WriteCanonicalNumber(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.TryGetDecimal(out var number))
        {
            writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
            return;
        }

        writer.WriteRawValue(element.GetRawText());
    }
}
