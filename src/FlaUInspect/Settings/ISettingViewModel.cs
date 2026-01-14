using FlaUInspect.Core;

namespace FlaUInspect.Settings;

public interface ISettingViewModel {
    Editable<FlaUiAppSettings> Settings { get; }
}