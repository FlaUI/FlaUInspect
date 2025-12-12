// // Description :    Definition of ElementOverlay.cs class
// //
// // Copyright © 2025 - 2025, Alcon. All rights reserved.

using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core.Overlay;

namespace FlaUInspect.Core;

public class ElementOverlay : IDisposable {
    public ElementOverlayConfiguration Configuration { get; }

    

    public ElementOverlay(ElementOverlayConfiguration configuration) {
        Configuration = configuration;

    }

    // public ElementOverlay(int size, int margin) {
    //     Size = size;
    //     Margin = margin;
    // }
    //
    // public ElementOverlay(int size, int margin, Color color) {
    //     Size = size;
    //     Margin = margin;
    //     Color = color;
    // }

    // public int Size { get; } = 2;
    //
    // public int Margin { get; } = 0;
    //
    // public Color Color { get; set; }
    
    public static Rectangle[] FillRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new Rectangle(rectangle.X - config.Margin, rectangle.Y - config.Margin, rectangle.Width + 2 * config.Margin, rectangle.Height + 2 * config.Margin)];
    }

    public static Rectangle[] BoundRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new (rectangle.X - config.Margin, rectangle.Y - config.Margin, config.Size, rectangle.Height + 2 * config.Margin),
            new (rectangle.X - config.Margin, rectangle.Y - config.Margin, rectangle.Width + 2 * config.Margin, config.Size),
            new (rectangle.X + rectangle.Width - config.Size + config.Margin, rectangle.Y - config.Margin, config.Size, rectangle.Height + 2 * config.Margin),
            new (rectangle.X - config.Margin, rectangle.Y + rectangle.Height - config.Size + config.Margin, rectangle.Width + 2 * config.Margin, config.Size)
        ];
    }

    public void Dispose() {
        Hide();
    }

    public void Hide() {
        foreach (OverlayRectangleForm overlayRectangleForm in _overlayRectangleFormList) {
            overlayRectangleForm.Hide();
            overlayRectangleForm.Close();
            overlayRectangleForm.Dispose();
        }
        _overlayRectangleFormList = [];
    }

    private OverlayRectangleForm[] _overlayRectangleFormList = [];

    public void Show(Rectangle rectangle) {
        Color color1 = Color.FromArgb(255, Configuration.Color.R, Configuration.Color.G, Configuration.Color.B);
        Rectangle[] rectangles = Configuration.RectangleFactory?.Invoke(Configuration, rectangle) ?? BoundRectangleFactory(Configuration, rectangle);
        // Rectangle[] rectangles = [
        //     new (rectangle.X - Configuration.Margin, rectangle.Y - Configuration.Margin, Configuration.Size, rectangle.Height + 2 * Configuration.Margin),
        //     new (rectangle.X - Configuration.Margin, rectangle.Y - Configuration.Margin, rectangle.Width + 2 * Configuration.Margin, Configuration.Size),
        //     new (rectangle.X + rectangle.Width - Configuration.Size + Configuration.Margin, rectangle.Y - Configuration.Margin, Configuration.Size, rectangle.Height + 2 * Configuration.Margin),
        //     new (rectangle.X - Configuration.Margin, rectangle.Y + rectangle.Height - Configuration.Size + Configuration.Margin, rectangle.Width + 2 * Configuration.Margin, Configuration.Size)
        // ];
        // Rectangle[] rectangles = [
        //     new (rectangle.X - Margin, rectangle.Y - Margin, rectangle.Width + Margin, rectangle.Height + Margin),
        // ];

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

public record class ElementOverlayConfiguration(int Size, int Margin, Color Color, Func<ElementOverlayConfiguration, Rectangle, Rectangle[]>? RectangleFactory = null);