namespace FlaUInspect.Core.Logger;

public class InternalLoggerMessage(LogLevel level, string message) {
    public LogLevel Level { get; } = level;
    public string Message { get; } = message;
}