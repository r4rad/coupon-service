using System.Text.RegularExpressions;

namespace CouponService.Infrastructure.Logging;

public static partial class SensitiveDataRedaction
{
    public const string RedactedToken = "[Redacted Token]";

    public const string RedactedConnectionString = "[Redacted Connection String]";

    public const string RedactedEmail = "[Redacted Email]";

    public static string RedactLogLine(string line) =>
        EmailPattern().Replace(
            ConnectionStringPattern().Replace(
                BearerTokenPattern().Replace(line, RedactedToken),
                RedactedConnectionString),
            RedactedEmail);

    public static bool ContainsSensitiveValue(string value) =>
        BearerTokenPattern().IsMatch(value)
        || ConnectionStringPattern().IsMatch(value)
        || EmailPattern().IsMatch(value);

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"(AccountKey|Password|SharedAccessKey|Secret)=([^;""\s]+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
