using System.Collections.Concurrent;

namespace FlaUInspect.Core.Logger;

public sealed class InternalLogger : ILogger {
    public ConcurrentBag<InternalLoggerMessage> Messages { get; } = [];

    public void Log(LogLevel level, string? message, params object?[] args) {
        if (message != null) {
            Messages.Add(new InternalLoggerMessage(level, string.Format(message, args)));
            OnLogEvent();
        }
    }

    public event EventHandler? LogEvent;


    private void OnLogEvent() {
        LogEvent?.Invoke(this, EventArgs.Empty);
    }
}