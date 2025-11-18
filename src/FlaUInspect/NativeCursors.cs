// // Description :    Definition of NativeCursors.cs class
// //
// // Copyright © 2025 - 2025, Alcon. All rights reserved.

namespace FlaUInspect;

using System;
using System.Runtime.InteropServices;

static class NativeCursors
{
    private const int IDC_CROSS = 32515;

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll")]
    static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    static extern int ShowCursor(bool bShow);

    public static void SetCross()
    {
        var cursor = LoadCursor(IntPtr.Zero, new IntPtr(IDC_CROSS));
        SetCursor(cursor);
        // Optionally hide the OS cursor (reference-counted)
        // ShowCursor(false);
    }

    public static void RestoreDefault()
    {
        // Load arrow and set it
        var arrow = LoadCursor(IntPtr.Zero, new IntPtr(32512)); // IDC_ARROW
        SetCursor(arrow);
        // ShowCursor(true);
    }
}
