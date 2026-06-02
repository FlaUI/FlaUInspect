using FlaUInspect.Core;

namespace FlaUInspect.Settings;

public class OverlaySettings : ObservableObject, ICloneable {
	public int Size {
		get;
		set => SetProperty(ref field, value);
	}
	public string Margin {
		get;
		set => SetProperty(ref field, value);
	} = "0";
	public string OverlayColor {
		get;
		set => SetProperty(ref field, value);
	} = "#FFFF0000";
	public string OverlayMode {
		get;
		set => SetProperty(ref field, value);
	} = "Bound";

	public object Clone() => new OverlaySettings {
		Size = Size,
		Margin = Margin,
		OverlayColor = OverlayColor,
		OverlayMode = OverlayMode
	};

	public void CoppyTo(OverlaySettings? to) {
		if (to == null)
			return;

		to.Size = Size;
		to.Margin = Margin;
		to.OverlayColor = OverlayColor;
		to.OverlayMode = OverlayMode;
	}
}