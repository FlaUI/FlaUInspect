using FlaUInspect.Core;

namespace FlaUInspect.Settings;

public class FlaUiAppSettings : ObservableObject, ICloneable {
    private OverlaySettings? _hoverOverlay = new ();
    private OverlaySettings? _pickOverlay = new ();
    private OverlaySettings? _selectionOverlay = new ();
    private string _theme = "Light";
    public string Theme {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public OverlaySettings? HoverOverlay {
        get => _hoverOverlay;
        set => SetProperty(ref _hoverOverlay, value);
    }

    public OverlaySettings? SelectionOverlay {
        get => _selectionOverlay;
        set => SetProperty(ref _selectionOverlay, value);
    }

    public OverlaySettings? PickOverlay {
        get => _pickOverlay;
        set => SetProperty(ref _pickOverlay, value);
    }

    public object Clone() {
        return new FlaUiAppSettings {
            Theme = Theme,
            HoverOverlay = HoverOverlay?.Clone() as OverlaySettings,
            SelectionOverlay = SelectionOverlay?.Clone() as OverlaySettings,
            PickOverlay = PickOverlay?.Clone() as OverlaySettings
        };
    }

    public void CopyTo(FlaUiAppSettings to) {
        to.Theme = Theme;
        to.PickOverlay?.CoppyTo(PickOverlay);
        to.SelectionOverlay?.CoppyTo(SelectionOverlay);
        to.HoverOverlay?.CoppyTo(HoverOverlay);
    }
}