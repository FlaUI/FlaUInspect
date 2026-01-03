using FlaUInspect.Core;
using FlaUInspect.Settings;

namespace FlaUInspect.ViewModels;

public interface ISettingViewModel {
    Editable<FlaUiAppSettings> Settings { get; }
}