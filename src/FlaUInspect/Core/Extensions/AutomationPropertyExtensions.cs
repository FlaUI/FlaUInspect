using FlaUI.Core;

namespace FlaUInspect.Core.Extensions;

public static class AutomationPropertyExtensions {
    public static string? ToDisplayText<T>(this IAutomationProperty<T?> automationProperty) {
        try {
            bool success = automationProperty.TryGetValue(out T? value);
            return success ? value == null ? string.Empty : value.ToString() : "Not Supported";
        }
        catch (Exception ex) {
            return $"Exception getting value ({ex.HResult})";
        }
    }
}