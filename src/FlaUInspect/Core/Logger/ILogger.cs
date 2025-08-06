namespace FlaUInspect.Core.Logger;

public interface ILogger {
    public void Log(LogLevel level, string? message, params object?[] args);
}