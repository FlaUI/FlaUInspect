using FlaUI.Core.Definitions;

namespace FlaUInspect.Controls;

public class FindCriteria {
    public string FindBy { get; set; } = "AutomationId";
    public string? TextValue { get; set; }
    public ControlType? ControlTypeValue { get; set; }
    public FlaUI.Core.FrameworkType? FrameworkTypeValue { get; set; }
    public bool IgnoreCase { get; set; }
    public bool SearchInChildrenOnly { get; set; }
    public bool SearchInLoadedOnly { get; set; }

    /// <summary>Returns true when there is nothing meaningful to search for.</summary>
    public bool IsEmpty => FindBy switch {
        "ControlType" or "FrameworkType" => false,
        _ => string.IsNullOrEmpty(TextValue)
    };
}
