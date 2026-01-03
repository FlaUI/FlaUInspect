using FlaUInspect.Core;
using FlaUInspect.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace FlaUInspect.ViewModels;

public class SettingsViewModel : ObservableObject, IDialogViewModel, ISettingViewModel {
    private readonly ISettingsService<FlaUiAppSettings> _settingsService;

    public SettingsViewModel() {
        _settingsService = App.Services.GetRequiredService<ISettingsService<FlaUiAppSettings>>();
        FlaUiAppSettings flaUiAppSettings = _settingsService.Load();
        Settings = new Editable<FlaUiAppSettings>(flaUiAppSettings,
                                                  s => (s.Clone() as FlaUiAppSettings)!,
                                                  (from, to) => from.CopyTo(to),
                                                  (a, b) => a.Equals(b));
    }

    public IEnumerable<string> Themes { get; } = new List<string> { "Light", "Dark" };
    public IEnumerable<string> OverlayModes { get; } = new List<string> { "Fill", "Border" };

    public void Save() {
        _settingsService.Save(Settings.Current);
    }

    public bool CanClose { get; } = true;

    public void Close() {

    }

    public Editable<FlaUiAppSettings> Settings { get; }
}