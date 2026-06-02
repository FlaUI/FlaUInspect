using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using FlaUI.Core.Overlay;
using FlaUInspect.Settings;

namespace FlaUInspect.Core;

public partial class ElementOverlay(ElementOverlayConfiguration configuration) : IDisposable {
	public ElementOverlay(OverlaySettings HoverOverlay) : this(new ElementOverlayConfiguration(HoverOverlay)) { }

	private OverlayRectangleForm[] _overlayRectangleFormList = [];

	public ElementOverlayConfiguration Configuration { get; } = configuration;

	public void Dispose() {
		Hide();
		GC.SuppressFinalize(this);
	}

	public void Hide() {
		foreach (var overlayRectangleForm in _overlayRectangleFormList) {
			try {
				overlayRectangleForm.Hide();
				overlayRectangleForm.Close();
			}
			catch (InvalidOperationException) { }
			overlayRectangleForm.Dispose();
		}
		_overlayRectangleFormList = [];
	}

	public void Show(Rectangle rectangle) {
		var color1 = Color.FromArgb(255, Configuration.Color.R, Configuration.Color.G, Configuration.Color.B);
		var rectangles = Configuration.RectangleFactory?.Invoke(Configuration, rectangle) ?? ElementOverlayConfiguration.BoundRectangleFactory(Configuration, rectangle);

		List<OverlayRectangleForm> rectangleForms = [];

		foreach (var rectangle1 in rectangles) {
			OverlayRectangleForm overlayRectangleForm1 = new() {
				BackColor = color1,
				Opacity = Configuration.Color.A / 255d
			};
			var overlayRectangleForm2 = overlayRectangleForm1;
			rectangleForms.Add(overlayRectangleForm2);
			_ = SetWindowPos(overlayRectangleForm2.Handle, new IntPtr(-1), rectangle1.X, rectangle1.Y, rectangle1.Width, rectangle1.Height, 16 /*0x10*/);
			_ = ShowWindow(overlayRectangleForm2.Handle, 8);
		}

		_overlayRectangleFormList = [.. rectangleForms];
	}

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool SetWindowPos(
		IntPtr hWnd,
		IntPtr hwndAfter,
		int x,
		int y,
		int width,
		int height,
		int flags);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

public record ElementOverlayConfiguration(int Size, Thickness Margin, Color Color, Func<ElementOverlayConfiguration, Rectangle, Rectangle[]>? RectangleFactory = null) {
	public ElementOverlayConfiguration(OverlaySettings HoverOverlay) : this(HoverOverlay.Size,
												(Thickness)(new ThicknessConverter().ConvertFromString(HoverOverlay.Margin) ?? new()),
												ColorTranslator.FromHtml(HoverOverlay.OverlayColor),
												GetRectangleFactory(HoverOverlay.OverlayMode)) { }

	public static Func<ElementOverlayConfiguration, Rectangle, Rectangle[]> GetRectangleFactory(string? mode) => mode?.ToLower(CultureInfo.InvariantCulture) switch {
		"fill" => FillRectangleFactory,
		"border" => BoundRectangleFactory,
		_ => BoundRectangleFactory
	};

	public static Rectangle[] FillRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) => [
			new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, rectangle.Width + (int)config.Margin.Right, rectangle.Height + (int)config.Margin.Bottom)
		];

	public static Rectangle[] BoundRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) => [
			new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
			new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, rectangle.Width + (int)config.Margin.Right, config.Size),
			new Rectangle(rectangle.X + rectangle.Width - config.Size + (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
			new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y + rectangle.Height - config.Size + (int)config.Margin.Right, rectangle.Width + (int)config.Margin.Right, config.Size)
		];
}