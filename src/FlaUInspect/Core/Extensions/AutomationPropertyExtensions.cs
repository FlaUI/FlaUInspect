using FlaUI.Core;

namespace FlaUInspect.Core.Extensions;

public static class AutomationPropertyExtensions {
	public static string? ToDisplayText<T>(this IAutomationProperty<T?> automationProperty) {
		try {
			return automationProperty.TryGetValue(out var value) ? value == null ? string.Empty : value.ToString() : "Not Supported";
		}
		catch (Exception ex) {
			return $"Exception getting value ({ex.HResult})";
		}
	}
}