using FlaUInspect.Core;
using System.Collections.ObjectModel;

namespace FlaUInspect.ViewModels;

public static class ExportConfiguration {
    public static ObservableCollection<ExportOptionItem> Options { get; } = GetOptions();

    private static ObservableCollection<ExportOptionItem> GetOptions() {
        var ret = new ObservableCollection<ExportOptionItem>();
        foreach (var itm in Enum.GetValues<JsonExporter.ExportOptions>()) {
            ret.Add(new ExportOptionItem { Header = itm.ToString(), IsChecked = JsonExporter.DefaultOptions.Contains(itm) });
        }
        return ret;
    }

}
