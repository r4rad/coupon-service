using System.Text.RegularExpressions;

namespace CouponService.Api.Observability;

public static partial class TraceParent
{
    public const string HeaderName = "traceparent";

    public static string Create(string traceId, string? parentId = null)
    {
        var spanId = parentId ?? RandomSpanId();
        return $"00-{traceId}-{spanId}-01";
    }

    public static bool TryParseTraceId(string? traceParent, out string traceId)
    {
        traceId = string.Empty;
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return false;
        }

        var parts = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "00", StringComparison.Ordinal) || parts[1].Length != 32)
        {
            return false;
        }

        if (!Hex32().IsMatch(parts[1]))
        {
            return false;
        }

        traceId = parts[1];
        return true;
    }

    private static string RandomSpanId()
    {
        Span<byte> bytes = stackalloc byte[8];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [GeneratedRegex("^[0-9a-fA-F]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex32();
}
