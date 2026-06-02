using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FlaUInspect.Core;

public static partial class GlobalMouseHook {
	private const int WH_MOUSE_LL = 14;
	private const int WM_MOUSEMOVE = 0x0200;
	private static IntPtr _hookId = 0;
	private static readonly LowLevelMouseProc _proc = HookCallback;

	public static event Action<int, int>? MouseMove; // your event

	public static void Start() => _hookId = SetHook(_proc);

	public static void Stop() => UnhookWindowsHookEx(_hookId);

	private static IntPtr SetHook(LowLevelMouseProc proc) {
		using var curProcess = Process.GetCurrentProcess();
		using var curModule = curProcess.MainModule!;
		return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
	}

	private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
		if (nCode >= 0 && wParam == WM_MOUSEMOVE) {
			var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
			MouseMove?.Invoke(data.pt.x, data.pt.y);
		}
		return CallNextHookEx(_hookId, nCode, wParam, lParam);
	}

	[LibraryImport("user32.dll")]
	private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
		IntPtr hMod, uint dwThreadId);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool UnhookWindowsHookEx(IntPtr hhk);

	[LibraryImport("user32.dll")]
	private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr GetModuleHandle(string lpModuleName);

	private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct MSLLHOOKSTRUCT {
		public POINT pt;
		public int mouseData;
		public int flags;
		public int time;
		public IntPtr dwExtraInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT {
		public int x;
		public int y;
	}
}