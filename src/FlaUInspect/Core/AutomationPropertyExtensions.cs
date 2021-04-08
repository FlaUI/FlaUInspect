using System;
using FlaUI.Core;

namespace FlaUInspect.Core
{
    public static class AutomationPropertyExtensions
    {
        public static string ToDisplayText<T>(this IAutomationProperty<T> automationProperty)
        {
            try
            {
                var success = automationProperty.TryGetValue(out T value);
                return success ? (value == null ? String.Empty : value.ToString()) : "Not Supported";
            }
            catch (Exception ex)
            {
                return $"Exception getting value ({ex.HResult})";
            }
        }
    }
}
