using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using FlaUI.Core.Overlay;

namespace FlaUInspect.Core;

public class ElementOverlay : IDisposable {

    private OverlayRectangleForm[] _overlayRectangleFormList = [];

    public ElementOverlay(ElementOverlayConfiguration configuration) {
        Configuration = configuration;

    }

    public ElementOverlayConfiguration Configuration { get; }

    public void Dispose() {
        Hide();
    }

    public static Func<ElementOverlayConfiguration, Rectangle, Rectangle[]> GetRectangleFactory(string mode) {
        return mode.ToLower() switch {
            "fill" => FillRectangleFactory,
            "border" => BoundRectangleFactory,
            _ => BoundRectangleFactory
        };
    }

    public static Rectangle[] FillRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new Rectangle(rectangle.X - (int)config.Margin.Left,
                          rectangle.Y - (int)config.Margin.Top,
                          rectangle.Width + (int)config.Margin.Right,
                          rectangle.Height + (int)config.Margin.Bottom)
        ];
    }

    public static Rectangle[] BoundRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, rectangle.Width + (int)config.Margin.Right, config.Size),
            new Rectangle(rectangle.X + rectangle.Width - config.Size + (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y + rectangle.Height - config.Size + (int)config.Margin.Right, rectangle.Width + (int)config.Margin.Right, config.Size)
        ];
    }

    public void Hide() {
        foreach (OverlayRectangleForm overlayRectangleForm in _overlayRectangleFormList) {
            overlayRectangleForm.Hide();
            overlayRectangleForm.Close();
            overlayRectangleForm.Dispose();
        }
        _overlayRectangleFormList = [];
    }

    public void Show(Rectangle rectangle) {
        Color color1 = Color.FromArgb(255, Configuration.Color.R, Configuration.Color.G, Configuration.Color.B);
        Rectangle[] rectangles = Configuration.RectangleFactory?.Invoke(Configuration, rectangle) ?? BoundRectangleFactory(Configuration, rectangle);

        List<OverlayRectangleForm> rectangleForms = [];

        foreach (Rectangle rectangle1 in rectangles) {
            OverlayRectangleForm overlayRectangleForm1 = new ();
            overlayRectangleForm1.BackColor = color1;
            overlayRectangleForm1.Opacity = Configuration.Color.A / 255d;
            OverlayRectangleForm overlayRectangleForm2 = overlayRectangleForm1;
            rectangleForms.Add(overlayRectangleForm2);
            SetWindowPos(overlayRectangleForm2.Handle, new IntPtr(-1), rectangle1.X, rectangle1.Y, rectangle1.Width, rectangle1.Height, 16 /*0x10*/);
            ShowWindow(overlayRectangleForm2.Handle, 8);
        }

        _overlayRectangleFormList = rectangleForms.ToArray();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hwndAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

public record ElementOverlayConfiguration(int Size, Thickness Margin, Color Color, Func<ElementOverlayConfiguration, Rectangle, Rectangle[]>? RectangleFactory = null);