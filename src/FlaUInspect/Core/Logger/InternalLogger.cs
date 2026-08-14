using System.Collections.Concurrent;

namespace FlaUInspect.Core.Logger;

public sealed class InternalLogger : ILogger {
    public ConcurrentBag<InternalLoggerMessage> Messages { get; } = [];

    public void Log(LogLevel level, string? message, params object?[] args) {
        if (message == null) {
            return;
        }

        // Only call string.Format when the caller supplied format arguments; otherwise use the
        // message as-is so braces in user data such as element names cannot trigger a FormatException.
        string formatted = args.Length > 0 ? string.Format(message, args) : message;
        Messages.Add(new InternalLoggerMessage(level, formatted));
        OnLogEvent();
    }

    public event EventHandler? LogEvent;

    private void OnLogEvent() {
        LogEvent?.Invoke(this, EventArgs.Empty);
    }
}
