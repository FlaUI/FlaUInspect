// // Description :    Definition of ElementOverlay.cs class
// //
// // Copyright © 2025 - 2025, Alcon. All rights reserved.

using System.Drawing;
using System.Runtime.InteropServices;
using FlaUI.Core.Overlay;

namespace FlaUInspect.Core;

public class ElementOverlay : IDisposable {
    public ElementOverlay() {
    }

    public ElementOverlay(Color color) {
        Color = color;
    }

    public ElementOverlay(int size, int margin) {
        Size = size;
        Margin = margin;
    }

    public ElementOverlay(int size, int margin, Color color) {
        Size = size;
        Margin = margin;
        Color = color;
    }

    public int Size { get; } = 2;

    public int Margin { get; } = 0;

    public Color Color { get; set; }

    public void Dispose() {
        foreach (OverlayRectangleForm overlayRectangleForm in _overlayRectangleFormList) {
            overlayRectangleForm.Hide();
            overlayRectangleForm.Close();
            overlayRectangleForm.Dispose();
        }
        _overlayRectangleFormList = [];
    }

    private OverlayRectangleForm[] _overlayRectangleFormList = [];

    public void CreateAndShowForms(Rectangle rectangle) {
        Color color1 = Color.FromArgb(255, Color.R, Color.G, Color.B);
        // Rectangle[] rectangles = [
        //     new (rectangle.X - Margin, rectangle.Y - Margin, Size, rectangle.Height + 2 * Margin),
        //     new (rectangle.X - Margin, rectangle.Y - Margin, rectangle.Width + 2 * Margin, Size),
        //     new (rectangle.X + rectangle.Width - Size + Margin, rectangle.Y - Margin, Size, rectangle.Height + 2 * Margin),
        //     new (rectangle.X - Margin, rectangle.Y + rectangle.Height - Size + Margin, rectangle.Width + 2 * Margin, Size)
        // ];
        Rectangle[] rectangles = [
            new (rectangle.X - Margin, rectangle.Y - Margin, rectangle.Width + Margin, rectangle.Height + Margin),
        ];

        List<OverlayRectangleForm> rectangleForms = [];

        foreach (Rectangle rectangle1 in rectangles) {
            OverlayRectangleForm overlayRectangleForm1 = new ();
            overlayRectangleForm1.BackColor = color1;
            overlayRectangleForm1.Opacity = Color.A / 255d;
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