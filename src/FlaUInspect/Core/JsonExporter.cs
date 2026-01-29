using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUInspect.Core.Extensions;

namespace FlaUInspect.Core;

public static class JsonExporter {
    public static ExportOptions[] DefaultOptions = new[]
    {
        ExportOptions.ControlType,
        ExportOptions.ClassName,
        ExportOptions.Name,
        ExportOptions.AutomationId
    };
    public enum ExportOptions {
        ControlType,
        ClassName,
        Name,
        AutomationId,
        HelpText,
        BoundingRectangle,
        ProcessId,
        IsEnabled,
        IsOffscreen,
        Value,
        SupportedPatterns
    }
    public class NodeInfo {
        public string? ControlType { get; set; }
        public string? ClassName { get; set; }
        public string? Name { get; set; }
        public string? AutomationId { get; set; }
        public string? HelpText { get; set; }
        public string? BoundingRectangle { get; set; }
        public int? ProcessId { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsOffscreen { get; set; }
        public string? Value { get; set; }
        public string[]? SupportedPatterns { get; set; }
        public List<NodeInfo>? Children { get; set; }
    }
    //"v => node.ControlType = v.ToString()"
    private static Regex LambdaToOptionKey = new(@"=>\s*node\.(?<optionKey>\w+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void SetPropertyOrNull<T>(HashSet<string> options, AutomationProperty<T> prop, Action<T> OnValue, [CallerArgumentExpression(nameof(OnValue))] string? OptionNameOverride = null) {
        try {
            var match = LambdaToOptionKey.Match(OptionNameOverride ?? string.Empty);
            if (match.Success)
                OptionNameOverride = match.Groups["optionKey"].Value;

            if (String.IsNullOrWhiteSpace(OptionNameOverride) || !options.Contains(OptionNameOverride))
                return;

            if (prop.TryGetValue(out var val))
                OnValue(val);
        } catch { }
    }
    public static string SerializeNodeInfo(NodeInfo node) {
        return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

    }
    public static NodeInfo CollectNodeData(AutomationElement element, HashSet<string> options) {
        var node = new NodeInfo();

        var props = element.Properties;

        SetPropertyOrNull(options, props.ControlType, v => node.ControlType = v.ToString());
        SetPropertyOrNull(options, props.ClassName, v => node.ClassName = v);
        SetPropertyOrNull(options, props.Name, v => node.Name = v);
        SetPropertyOrNull(options, props.AutomationId, v => node.AutomationId = v);
        SetPropertyOrNull(options, props.HelpText, v => node.HelpText = v);
        SetPropertyOrNull(options, props.BoundingRectangle, v => node.BoundingRectangle = v.ToString());
        SetPropertyOrNull(options, props.ProcessId, v => node.ProcessId = v);
        SetPropertyOrNull(options, props.IsEnabled, v => node.IsEnabled = v);
        SetPropertyOrNull(options, props.IsOffscreen, v => node.IsOffscreen = v);
        try {
            if (options.Contains("Value") && element.Patterns.Value.TryGetPattern(out var valuePattern)) {
                SetPropertyOrNull(options, valuePattern.Value, v => node.Value = v);
            }
        } catch { }

        if (options.Contains("SupportedPatterns")) {
            try {
                var patterns = element.GetSupportedPatterns();
                node.SupportedPatterns = patterns.Select(p => p.Name)
                    .Where(n => n != "LegacyIAccessible" && n != "LegacyIAccessiblePattern")
                    .OrderBy(x => x).ToArray();
            } catch { }
        }

        var children = new List<NodeInfo>();
        try {
            foreach (var child in element.FindAllChildren()) {
                children.Add(CollectNodeData(child, options));
            }
        } catch { } // Ignore errors fetching children

        if (children.Count > 0) {
            node.Children = children;
        }

        return node;
    }
}
