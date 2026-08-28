using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;

namespace CouponService.Infrastructure.Logging;

public sealed class CollectingLogSink : ILogEventSink, IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];
    private readonly ITextFormatter _formatter = new RedactingCompactJsonFormatter();

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate)
            {
                return _lines.ToArray();
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        lock (_gate)
        {
            _lines.Add(writer.ToString());
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
        }
    }

    public void Dispose() => Clear();
}
