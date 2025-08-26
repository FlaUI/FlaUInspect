using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using FlaUI.Core.Patterns;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using FlaUI.UIA3.Identifiers;
using FlaUI.UIA3.Patterns;
using FlaUInspect.ViewModels;
using GridItemPattern = FlaUI.UIA2.Patterns.GridItemPattern;
using GridPattern = FlaUI.UIA2.Patterns.GridPattern;
using RangeValuePattern = FlaUI.UIA2.Patterns.RangeValuePattern;
using ScrollPattern = FlaUI.UIA2.Patterns.ScrollPattern;
using SelectionItemPattern = FlaUI.UIA2.Patterns.SelectionItemPattern;
using SelectionPattern = FlaUI.UIA2.Patterns.SelectionPattern;
using TableItemPattern = FlaUI.UIA2.Patterns.TableItemPattern;
using TablePattern = FlaUI.UIA2.Patterns.TablePattern;
using TextPattern = FlaUI.UIA2.Patterns.TextPattern;
using TogglePattern = FlaUI.UIA2.Patterns.TogglePattern;
using ValuePattern = FlaUI.UIA2.Patterns.ValuePattern;
using WindowPattern = FlaUI.UIA2.Patterns.WindowPattern;

namespace FlaUInspect.Core;

public class PatternItemsFactory(AutomationBase? automationBase) {
    public const string Identification = "Identification";
    public const string Details = "Details";
    public const string PatternSupport = "Pattern Support";

    private readonly KeyValuePair<PatternId, Func<AutomationElement, IEnumerable<PatternItem>>>[] _patternsUia2Func =
    [
        new (GridItemPattern.Pattern, AddGridItemPatternDetails),
        new (GridPattern.Pattern, AddGridPatternPatternDetails),
        new (RangeValuePattern.Pattern, AddRangeValuePatternDetails),
        new (ScrollPattern.Pattern, AddScrollPatternDetails),
        new (SelectionItemPattern.Pattern, AddSelectionItemPatternDetails),
        new (SelectionPattern.Pattern, AddSelectionPatternDetails),
        new (TableItemPattern.Pattern, AddTableItemPatternDetails),
        new (TablePattern.Pattern, AddTablePatternDetails),
        new (TextPattern.Pattern, AddTextPatternDetails),
        new (TogglePattern.Pattern, AddTogglePatternDetails),
        new (ValuePattern.Pattern, AddValuePatternDetails),
        new (WindowPattern.Pattern, AddWindowPatternDetails),
        new (InvokePattern.Pattern, AddInvokePatternDetails)
    ];

    private readonly KeyValuePair<PatternId, Func<AutomationElement, IEnumerable<PatternItem>>>[] _patternsUia3Func =
    [
        new (FlaUI.UIA3.Patterns.GridItemPattern.Pattern, AddGridItemPatternDetails),
        new (FlaUI.UIA3.Patterns.GridPattern.Pattern, AddGridPatternPatternDetails),
        new (LegacyIAccessiblePattern.Pattern, AddLegacyIAccessiblePatternDetails),
        new (FlaUI.UIA3.Patterns.RangeValuePattern.Pattern, AddRangeValuePatternDetails),
        new (FlaUI.UIA3.Patterns.ScrollPattern.Pattern, AddScrollPatternDetails),
        new (FlaUI.UIA3.Patterns.SelectionItemPattern.Pattern, AddSelectionItemPatternDetails),
        new (FlaUI.UIA3.Patterns.SelectionPattern.Pattern, AddSelectionPatternDetails),
        new (FlaUI.UIA3.Patterns.TableItemPattern.Pattern, AddTableItemPatternDetails),
        new (FlaUI.UIA3.Patterns.TablePattern.Pattern, AddTablePatternDetails),
        new (FlaUI.UIA3.Patterns.TextPattern.Pattern, AddTextPatternDetails),
        new (FlaUI.UIA3.Patterns.TogglePattern.Pattern, AddTogglePatternDetails),
        new (FlaUI.UIA3.Patterns.ValuePattern.Pattern, AddValuePatternDetails),
        new (FlaUI.UIA3.Patterns.WindowPattern.Pattern, AddWindowPatternDetails),
        new (FlaUI.UIA3.Patterns.InvokePattern.Pattern, AddInvokePatternDetails)
    ];

    public IDictionary<string, PatternItem[]> CreatePatternItemsForElement(AutomationElement element, HashSet<PatternId> allSupportedPatterns) {
        Dictionary<string, PatternItem[]> patternItems = new()
        {
            { Identification, AddIdentificationDetails(element).ToArray() },
            { Details, AddDetailsDetails(element).ToArray() },
            { PatternSupport, AddPatternSupportDetails(element).ToArray() }
        };

        KeyValuePair<PatternId, Func<AutomationElement, IEnumerable<PatternItem>>>[] patternsFactory =
            automationBase is UIA3Automation ? _patternsUia3Func : _patternsUia2Func;

        foreach ((PatternId key, Func<AutomationElement, IEnumerable<PatternItem>> value) in patternsFactory.Where(kvp => allSupportedPatterns.Contains(kvp.Key))) {
            patternItems.Add(key.Name, (value.Invoke(element)).ToArray());
        }

        return patternItems;
    }

    private static IEnumerable<PatternItem> AddPatternSupportDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        PatternId[] allSupportedPatterns = element.GetSupportedPatterns();
        PatternId[] allPatterns = element.Automation.PatternLibrary.AllForCurrentFramework;

        foreach (PatternId pattern in allPatterns) {
            yield return new PatternItem(pattern.Name, allSupportedPatterns.Any(x => x.Name == pattern.Name) ? "Yes" : "No");
        }
    }

    private static IEnumerable<PatternItem> AddValuePatternDetails(AutomationElement element) {
        IValuePattern pattern = element.Patterns.Value.Pattern;
        yield return PatternItem.FromAutomationProperty("IsReadOnly", pattern.IsReadOnly);
        yield return PatternItem.FromAutomationProperty("Value", pattern.Value);
    }

    private static IEnumerable<PatternItem> AddTogglePatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ITogglePattern pattern = element.Patterns.Toggle.Pattern;
        yield return PatternItem.FromAutomationProperty("ToggleState", pattern.ToggleState);
    }

    private static IEnumerable<PatternItem> AddTextPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ITextPattern pattern = element.Patterns.Text.Pattern;

        object mixedValue = element.AutomationType == AutomationType.UIA2
            ? System.Windows.Automation.TextPattern.MixedAttributeValue
            : ((UIA3Automation)element.Automation).NativeAutomation.ReservedMixedAttributeValue;

        string foreColor = GetTextAttribute<int>(element, pattern, TextAttributes.ForegroundColor, mixedValue, (x) => $"{Color.FromArgb(x)} ({x})");
        string backColor = GetTextAttribute<int>(element, pattern, TextAttributes.BackgroundColor, mixedValue, (x) => $"{Color.FromArgb(x)} ({x})");
        string fontName = GetTextAttribute<string>(element, pattern, TextAttributes.FontName, mixedValue, (x) => $"{x}");
        string fontSize = GetTextAttribute<double>(element, pattern, TextAttributes.FontSize, mixedValue, (x) => $"{x}");
        string fontWeight = GetTextAttribute<int>(element, pattern, TextAttributes.FontWeight, mixedValue, (x) => $"{x}");


        yield return new PatternItem("ForeColor", foreColor);
        yield return new PatternItem("BackgroundColor", backColor);
        yield return new PatternItem("FontName", fontName);
        yield return new PatternItem("FontSize", fontSize);
        yield return new PatternItem("FontWeight", fontWeight);
    }

    private static IEnumerable<PatternItem> AddTablePatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ITablePattern pattern = element.Patterns.Table.Pattern;
        yield return PatternItem.FromAutomationProperty("ColumnHeaderItems", pattern.ColumnHeaders);
        yield return PatternItem.FromAutomationProperty("RowHeaderItems", pattern.RowHeaders);
        yield return PatternItem.FromAutomationProperty("RowOrColumnMajor", pattern.RowOrColumnMajor);
    }

    private static IEnumerable<PatternItem> AddTableItemPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ITableItemPattern pattern = element.Patterns.TableItem.Pattern;
        yield return PatternItem.FromAutomationProperty("ColumnHeaderItems", pattern.ColumnHeaderItems);
        yield return PatternItem.FromAutomationProperty("RowHeaderItems", pattern.RowHeaderItems);
    }

    private static IEnumerable<PatternItem> AddSelectionPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ISelectionPattern pattern = element.Patterns.Selection.Pattern;
        yield return PatternItem.FromAutomationProperty("Selection", pattern.Selection);
        yield return PatternItem.FromAutomationProperty("CanSelectMultiple", pattern.CanSelectMultiple);
        yield return PatternItem.FromAutomationProperty("IsSelectionRequired", pattern.IsSelectionRequired);
    }

    private static IEnumerable<PatternItem> AddSelectionItemPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ISelectionItemPattern pattern = element.Patterns.SelectionItem.Pattern;
        yield return PatternItem.FromAutomationProperty("IsSelected", pattern.IsSelected);
        yield return PatternItem.FromAutomationProperty("SelectionContainer", pattern.SelectionContainer);
    }

    private static IEnumerable<PatternItem> AddScrollPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        IScrollPattern pattern = element.Patterns.Scroll.Pattern;
        yield return PatternItem.FromAutomationProperty("HorizontalScrollPercent", pattern.HorizontalScrollPercent);
        yield return PatternItem.FromAutomationProperty("VerticalScrollPercent", pattern.VerticalScrollPercent);
        yield return PatternItem.FromAutomationProperty("HorizontalViewSize", pattern.HorizontalViewSize);
        yield return PatternItem.FromAutomationProperty("VerticalViewSize", pattern.VerticalViewSize);
        yield return PatternItem.FromAutomationProperty("HorizontallyScrollable", pattern.HorizontallyScrollable);
        yield return PatternItem.FromAutomationProperty("VerticallyScrollable", pattern.VerticallyScrollable);
    }

    private static IEnumerable<PatternItem> AddRangeValuePatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        IRangeValuePattern pattern = element.Patterns.RangeValue.Pattern;
        yield return PatternItem.FromAutomationProperty("IsReadOnly", pattern.IsReadOnly);
        yield return PatternItem.FromAutomationProperty("LargeChange", pattern.LargeChange);
        yield return PatternItem.FromAutomationProperty("Maximum", pattern.Maximum);
        yield return PatternItem.FromAutomationProperty("Minimum", pattern.Minimum);
        yield return PatternItem.FromAutomationProperty("SmallChange", pattern.SmallChange);
        yield return PatternItem.FromAutomationProperty("Value", pattern.Value);
    }

    private static IEnumerable<PatternItem> AddLegacyIAccessiblePatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        ILegacyIAccessiblePattern pattern = element.Patterns.LegacyIAccessible.Pattern;
        yield return PatternItem.FromAutomationProperty("Name", pattern.Name);
        yield return new PatternItem("State",
                                     AccessibilityTextResolver.GetStateText(pattern.State.ValueOrDefault));
        yield return new PatternItem("Role",
                                     AccessibilityTextResolver.GetRoleText(pattern.Role.ValueOrDefault));
        yield return PatternItem.FromAutomationProperty("Value", pattern.Value);
        yield return PatternItem.FromAutomationProperty("ChildId", pattern.ChildId);
        yield return PatternItem.FromAutomationProperty("DefaultAction", pattern.DefaultAction);
        yield return PatternItem.FromAutomationProperty("Description", pattern.Description);
        yield return PatternItem.FromAutomationProperty("Help", pattern.Help);
        yield return PatternItem.FromAutomationProperty("KeyboardShortcut", pattern.KeyboardShortcut);
        // Commented out because Selection throws an exception or freezes the UI
        // yield return PatternItem.FromAutomationProperty("Selection", pattern.Selection);
    }

    private static IEnumerable<PatternItem> AddGridPatternPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }
        IGridPattern pattern = element.Patterns.Grid.Pattern;
        yield return PatternItem.FromAutomationProperty("ColumnCount", pattern.ColumnCount);
        yield return PatternItem.FromAutomationProperty("RowCount", pattern.RowCount);
    }

    private static IEnumerable<PatternItem> AddDetailsDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }

        // Element details
        yield return PatternItem.FromAutomationProperty("IsEnabled", element.Properties.IsEnabled);
        yield return PatternItem.FromAutomationProperty("IsOffscreen", element.Properties.IsOffscreen);
        yield return PatternItem.FromAutomationProperty("BoundingRectangle", element.Properties.BoundingRectangle);
        yield return PatternItem.FromAutomationProperty("HelpText", element.Properties.HelpText);
        yield return PatternItem.FromAutomationProperty("IsPassword", element.Properties.IsPassword);
        // Special handling for NativeWindowHandle
        IntPtr nativeWindowHandle = element.Properties.NativeWindowHandle.ValueOrDefault;
        var nativeWindowHandleString = "Not Supported";

        if (nativeWindowHandle != 0) {
            nativeWindowHandleString = string.Format("{0} ({0:X8})", nativeWindowHandle.ToInt32());
        }
        yield return new PatternItem("NativeWindowHandle", nativeWindowHandleString);
    }

    private static IEnumerable<PatternItem> AddIdentificationDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }

        yield return PatternItem.FromAutomationProperty("AutomationId", element.Properties.AutomationId);
        yield return PatternItem.FromAutomationProperty("Name", element.Properties.Name);
        yield return PatternItem.FromAutomationProperty("ClassName", element.Properties.ClassName);
        yield return PatternItem.FromAutomationProperty("ControlType", element.Properties.ControlType);
        yield return PatternItem.FromAutomationProperty("LocalizedControlType", element.Properties.LocalizedControlType);
        yield return new PatternItem("FrameworkType", element.FrameworkType.ToString());
        yield return PatternItem.FromAutomationProperty("FrameworkId", element.Properties.FrameworkId);
        yield return PatternItem.FromAutomationProperty("ProcessId", element.Properties.ProcessId);
    }

    private static IEnumerable<PatternItem> AddWindowPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }

        IWindowPattern pattern = element.Patterns.Window.Pattern;
        yield return new PatternItem("IsModal", pattern.IsModal.ToString());
        yield return new PatternItem("IsTopmost", pattern.IsTopmost.ToString());
        yield return new PatternItem("CanMinimize", pattern.CanMinimize.ToString());
        yield return new PatternItem("CanMaximize", pattern.CanMaximize.ToString());
        yield return new PatternItem("WindowVisualState", pattern.WindowVisualState.ToString());
        yield return new PatternItem("WindowInteractionState", pattern.WindowInteractionState.ToString());
    }

    private static IEnumerable<PatternItem> AddGridItemPatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }

        IGridItemPattern pattern = element.Patterns.GridItem.Pattern;
        yield return new PatternItem("Column", pattern.Column.ToString());
        yield return new PatternItem("ColumnSpan", pattern.ColumnSpan.ToString());
        yield return new PatternItem("Row", pattern.Row.ToString());
        yield return new PatternItem("RowSpan", pattern.RowSpan.ToString());
        yield return new PatternItem("ContainingGrid", pattern.ContainingGrid.ToString());
    }

    private static IEnumerable<PatternItem> AddInvokePatternDetails(AutomationElement? element) {
        if (element == null) {
            yield break;
        }

        IInvokePattern pattern = element.Patterns.Invoke.Pattern;
        yield return new PatternItem("Invoke", "Invoke", pattern.Invoke);
    }

    private static string GetTextAttribute<T>(AutomationElement element, ITextPattern pattern,
        TextAttributeId textAttribute, object mixedValue, Func<T, string> func) {
        object value = pattern.DocumentRange.GetAttributeValue(textAttribute);

        if (value == mixedValue) {
            return "Mixed";
        }

        if (value == element.Automation.NotSupportedValue) {
            return "Not supported";
        }

        try {
            T converted = (T)value;
            return func(converted);
        } catch {
            return $"Conversion to ${typeof(T)} failed";
        }
    }
}