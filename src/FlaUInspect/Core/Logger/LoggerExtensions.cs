using System.Text.RegularExpressions;

namespace FlaUInspect.Core.Logger;

public static partial class LoggerExtensions {
	public static void LogInformation(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Information, message, args);

	public static void LogWarning(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Warning, message, args);

	public static void LogError(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Error, message, args);

	public static void LogCritical(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Critical, message, args);

	public static void LogDebug(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Debug, message, args);

	public static void LogTrace(this ILogger logger, string? message, params object?[] args) => logger.Log(LogLevel.Trace, message, args);

	public static string? CreateLogMessage(string? message, params object?[] args) {
		var formattedMessage = string.IsNullOrEmpty(message) ? string.Empty : message;

		if (string.IsNullOrEmpty(formattedMessage))
			return string.Empty;

		var paramsName = Wordlike().Matches(formattedMessage)
									   .Select(x => x.Groups[1].Value)
									   .Where(x => !string.IsNullOrEmpty(x))
									   .ToList();

		for (var i = 0; i < paramsName.Count; i++)
			if (i < args.Length)
				formattedMessage = formattedMessage.Replace(paramsName[i], args[i]?.ToString());

		return formattedMessage;
	}

	[GeneratedRegex(@"(\{\w+\})")]
	private static partial Regex Wordlike();
}