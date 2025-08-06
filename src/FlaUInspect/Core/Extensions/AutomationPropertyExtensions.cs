using FlaUI.Core;

namespace FlaUInspect.Core.Extensions;

public static class AutomationPropertyExtensions {
    public static string? ToDisplayText<T>(this IAutomationProperty<T?> automationProperty) {
        try {
#pragma warning disable CA1416
            bool success = automationProperty.TryGetValue(out T? value);
#pragma warning restore CA1416
            return success ? value == null ? string.Empty : value.ToString() : "Not Supported";
        }
        catch (Exception ex) {
            return $"Exception getting value ({ex.HResult})";
        }
    }
}