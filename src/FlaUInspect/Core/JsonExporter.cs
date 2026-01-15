using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUInspect.Core.Extensions;

namespace FlaUInspect.Core;

public static class JsonExporter
{
    public static Dictionary<string, object> CollectNodeData(AutomationElement element, HashSet<string>? options = null)
    {
        var dict = new Dictionary<string, object>();
        // Default options if none provided
        options ??= new HashSet<string> { "ControlType", "ClassName", "Name", "AutomationId" };

        if (options.Contains("ControlType")) dict["ControlType"] = element.Properties.ControlType.ValueOrDefault.ToString();
        if (options.Contains("ClassName")) dict["ClassName"] = element.Properties.ClassName.ValueOrDefault;
        if (options.Contains("Name")) dict["Name"] = element.Properties.Name.ValueOrDefault;
        if (options.Contains("AutomationId")) dict["AutomationId"] = element.Properties.AutomationId.ValueOrDefault;
        if (options.Contains("HelpText")) dict["HelpText"] = element.Properties.HelpText.ValueOrDefault;
        if (options.Contains("BoundingRectangle")) dict["BoundingRectangle"] = element.Properties.BoundingRectangle.ValueOrDefault.ToString();
        if (options.Contains("ProcessId")) dict["ProcessId"] = element.Properties.ProcessId.ValueOrDefault;
        if (options.Contains("IsEnabled")) dict["IsEnabled"] = element.Properties.IsEnabled.ValueOrDefault;
        if (options.Contains("IsOffscreen")) dict["IsOffscreen"] = element.Properties.IsOffscreen.ValueOrDefault;

        if (options.Contains("SupportedPatterns")) {
            try {
                var patterns = element.GetSupportedPatterns();
                dict["SupportedPatterns"] = patterns.Select(p => p.Name)
                    .Where(n => n != "LegacyIAccessible" && n != "LegacyIAccessiblePattern")
                    .OrderBy(x => x).ToArray();
            } catch { }
        }

        var children = new List<Dictionary<string, object>>();
        try {
            foreach (var child in element.FindAllChildren()) {
                children.Add(CollectNodeData(child, options));
            }
        } catch { } // Ignore errors fetching children

        if (children.Count > 0) {
            dict["Children"] = children;
        }

        return dict;
    }
}
