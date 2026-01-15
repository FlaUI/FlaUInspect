using FlaUInspect.Core;

namespace FlaUInspect.ViewModels;

public class ExportOptionItem : ObservableObject
{
    public string Header { get; set; } = string.Empty;

    public bool IsChecked
    {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }
}
