using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using FlaUInspect.Core.Logger;

namespace FlaUInspect.Core;

public static class ElementHighlighter {
    public static void HighlightElement(AutomationElement? automationElement, ILogger logger) {
        try {
            Task.Run(() => automationElement?.DrawHighlight(false, Color.Red, TimeSpan.FromSeconds(1)));
        }
        catch (PropertyNotSupportedException ex) {
            logger.LogError($"Exception: {ex.Message}");
        }
    }
}