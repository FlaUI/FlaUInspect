using System.Collections.ObjectModel;

namespace FlaUInspect.ViewModels;

public static class ExportConfiguration
{
    public static ObservableCollection<ExportOptionItem> Options { get; } = new()
    {
        new ExportOptionItem { Header = "ControlType", IsChecked = true },
        new ExportOptionItem { Header = "ClassName", IsChecked = true },
        new ExportOptionItem { Header = "Name", IsChecked = true },
        new ExportOptionItem { Header = "AutomationId", IsChecked = true },
        new ExportOptionItem { Header = "HelpText", IsChecked = false },
        new ExportOptionItem { Header = "BoundingRectangle", IsChecked = false },
        new ExportOptionItem { Header = "ProcessId", IsChecked = false },
        new ExportOptionItem { Header = "IsEnabled", IsChecked = false },
        new ExportOptionItem { Header = "IsOffscreen", IsChecked = false },
        new ExportOptionItem { Header = "SupportedPatterns", IsChecked = false }
    };
}
