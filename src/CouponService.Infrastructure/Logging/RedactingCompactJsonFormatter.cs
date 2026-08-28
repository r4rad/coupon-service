using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace CouponService.Infrastructure.Logging;

// AC-8.4: scrub sensitive values from every emitted JSON line.
public sealed class RedactingCompactJsonFormatter : ITextFormatter
{
    private readonly CompactJsonFormatter _inner = new();

    public void Format(LogEvent logEvent, TextWriter output)
    {
        using var buffer = new StringWriter();
        _inner.Format(logEvent, buffer);
        output.Write(SensitiveDataRedaction.RedactLogLine(buffer.ToString()));
    }
}
