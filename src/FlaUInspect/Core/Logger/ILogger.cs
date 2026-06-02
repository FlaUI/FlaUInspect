namespace FlaUInspect.Core.Logger;

public interface ILogger {
	void Log(LogLevel level, string? message, params object?[] args);
}