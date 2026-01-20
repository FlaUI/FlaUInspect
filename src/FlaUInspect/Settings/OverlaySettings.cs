using FlaUInspect.Core;

namespace FlaUInspect.Settings;

public class OverlaySettings : ObservableObject, ICloneable {
    private string _color = "#FFFF0000";
    private string _margin = "0";
    private string _overlayMode = "Bound";
    private int _size;
    public int Size {
        get => _size;
        set => SetProperty(ref _size, value);
    }
    public string Margin {
        get => _margin;
        set => SetProperty(ref _margin, value);
    }
    public string OverlayColor {
        get => _color;
        set => SetProperty(ref _color, value);
    }
    public string OverlayMode {
        get => _overlayMode;
        set => SetProperty(ref _overlayMode, value);
    }

    public object Clone() {
        return new OverlaySettings {
            Size = Size,
            Margin = Margin,
            OverlayColor = OverlayColor,
            OverlayMode = OverlayMode
        };
    }

    public void CoppyTo(OverlaySettings? to) {
        if (to == null) {
            return;
        }
        to.Size = Size;
        to.Margin = Margin;
        to.OverlayColor = OverlayColor;
        to.OverlayMode = OverlayMode;
    }
}