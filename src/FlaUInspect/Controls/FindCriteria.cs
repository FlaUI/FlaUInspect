using FlaUI.Core.Definitions;

namespace FlaUInspect.Controls;

/// <summary>How the text value is compared against the element property.</summary>
public enum SearchMatchMode {
    /// <summary>Exact string equality, case-sensitive.</summary>
    Exact = 0,
    /// <summary>Case-insensitive substring (Contains).</summary>
    IgnoreCase = 1,
    /// <summary>Case-sensitive substring (Contains) — default.</summary>
    Substring = 2
}

public class FindCriteria {
    public string FindBy { get; set; } = "AutomationId";
    public string? TextValue { get; set; }
    public ControlType? ControlTypeValue { get; set; }
    public FlaUI.Core.FrameworkType? FrameworkTypeValue { get; set; }
    public SearchMatchMode MatchMode { get; set; } = SearchMatchMode.Substring;
    public bool SearchInChildrenOnly { get; set; }
    public bool SearchInLoadedOnly { get; set; }

    /// <summary>Returns true when there is nothing meaningful to search for.</summary>
    public bool IsEmpty => FindBy switch {
        "ControlType" or "FrameworkType" => false,
        _ => string.IsNullOrEmpty(TextValue)
    };
}
