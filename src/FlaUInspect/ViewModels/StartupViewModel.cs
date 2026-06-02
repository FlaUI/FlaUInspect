using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaUInspect.Core;
using FlaUInspect.Settings;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;

namespace FlaUInspect.ViewModels;

public partial class StartupViewModel : ObservableObject, IDisposable {
	private const int WhMouseLl = 14;
	private const uint GaRoot = 2;

	private static IntPtr _mouseHook = 0;
	private static LowLevelMouseProc? _mouseProc;

	private readonly AutomationBase _defaultAutomation = new UIA3Automation();
	private ElementOverlay? _topWindowOverlay;
	private AutomationElement? _topWindowUnderCursor;
	private bool _disposedValue;

	public StartupViewModel() {
		IsWindowedOnly = true;
		RefreshCommand = new AsyncRelayCommand(_ => Init());

		PickCommand = new AsyncRelayCommand(async _ => {
			using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
			var hwnd = await PickWindowAsync(cts.Token);
			SelectedProcess = Processes.FirstOrDefault(x => x.MainWindowHandle == hwnd);
		});

		SettingCommand = new RelayCommand(_ => {
			var settingsService = App.Services.GetService<ISettingsService<FlaUiAppSettings>>();
			if (settingsService is null)
				return;
			var flaUiAppSettings = settingsService.Load();
			Editable<FlaUiAppSettings> settings = new(flaUiAppSettings,
													   s => (FlaUiAppSettings)s.Clone(),
													   (from, to) => from.CopyTo(to),
													   (a, b) => a.Equals(b));

			DialogContent = new SettingsViewModel();
		});

		CloseSettingCommand = new RelayCommand(_ => {
			((IDialogViewModel)DialogContent!).Close();
			DialogContent = null;
		},
											   _ => DialogContent is IDialogViewModel { CanClose: true });

		SaveSettingCommand = new RelayCommand(_ => {
			((IDialogViewModel)DialogContent!).Save();

			var settingsViewModel = DialogContent as ISettingViewModel;
			DialogContent = null;

			if (settingsViewModel != null)
				App.ApplyAppOption(settingsViewModel.Settings.Current);
		},
											  _ => DialogContent is IDialogViewModel { CanClose: true });
		AboutCommand = new RelayCommand(_ => DialogContent = new AboutViewModel());

		FilteredProcesses = CollectionViewSource.GetDefaultView(Processes);
		FilteredProcesses.Filter = FilterProcesses;

		DialogContent = null;
	}

	public object? DialogContent {
		get => GetProperty<object?>();
		set => SetProperty(value);
	}

	public ICollectionView FilteredProcesses { get; private set; }

	public ICommand SettingCommand { get; private set; }
	public ICommand RefreshCommand { get; private set; }
	public ICommand PickCommand { get; }

	public ICommand CloseSettingCommand { get; }
	public ICommand SaveSettingCommand { get; }

	public ICommand AboutCommand { get; }

	public bool IsBusy {
		get => GetProperty<bool>();
		set => SetProperty(value);
	}

	public ProcessWindowInfo? SelectedProcess {
		get => GetProperty<ProcessWindowInfo>();
		set => SetProperty(value);
	}

	public ObservableCollection<ProcessWindowInfo> Processes {
		get;
		private set {
			_ = SetProperty(ref field, value);
			FilteredProcesses = CollectionViewSource.GetDefaultView(field);
			FilteredProcesses.Filter = FilterProcesses;
			OnPropertyChanged(nameof(FilteredProcesses));
		}
	} = [];

	public string? FilterProcess {
		get => GetProperty<string?>();
		set {
			if (SetProperty(value))
				FilteredProcesses?.Refresh();
		}
	}

	public bool IsWindowedOnly {
		get => GetProperty<bool>();
		set {
			if (SetProperty(value))
				_ = Task.Run(Init);
		}
	}

	private bool FilterProcesses(object obj) => obj is ProcessWindowInfo p && (
			string.IsNullOrWhiteSpace(FilterProcess)
			|| p.WindowTitle.Contains(FilterProcess, StringComparison.OrdinalIgnoreCase)
			|| p.ProcessId.ToString(CultureInfo.InvariantCulture).Contains(FilterProcess, StringComparison.OrdinalIgnoreCase));

	private async Task<IntPtr> PickWindowAsync(CancellationToken ctsToken) {
		var previousCursor = Mouse.OverrideCursor;
		Mouse.OverrideCursor = Cursors.Cross;

		try {
			return await WaitForMouseClickWindowAsync(ctsToken);
		}
		catch (OperationCanceledException) {
			// canceled - ignore
		}
		finally {
			_ = Application.Current.Dispatcher.Invoke(() => Mouse.OverrideCursor = previousCursor);
		}
		return 0;
	}

	private Task<IntPtr> WaitForMouseClickWindowAsync(CancellationToken ct) {
		TaskCompletionSource<IntPtr> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

		_mouseProc = (nCode, wParam, lParam) => {
			const int WM_LBUTTONDOWN = 0x0201;
			const int WM_LBUTTONUP = 0x0202;

			if (nCode < 0 || (wParam != WM_LBUTTONUP && wParam != WM_LBUTTONDOWN && wParam != 0x0200) || !GetCursorPos(out var pt))
				return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

			var hwnd = WindowFromPoint(pt);
			var root = GetAncestor(hwnd, GaRoot);

			// Highlight the window under the mouse, but skip if it is the current process
			if (GetWindowThreadProcessId(root, out var windowProcessId) == (uint)Environment.ProcessId) {
				_topWindowOverlay?.Dispose();
				_topWindowUnderCursor = null;
			}
			else {
				var topWindowUnderCursor = GetTopWindowUnderCursor();

				if (_topWindowUnderCursor == null || !_topWindowUnderCursor.Equals(topWindowUnderCursor)) {
					_topWindowOverlay?.Dispose();
					try {
						var boundingRectangleValue = topWindowUnderCursor?.Properties.BoundingRectangle.Value ?? new();
						_topWindowOverlay = App.FlaUiAppOptions.PickOverlay();
						_topWindowOverlay?.Show(boundingRectangleValue);
						_topWindowUnderCursor = topWindowUnderCursor;

						if (topWindowUnderCursor != null)
							SelectedProcess = Processes.FirstOrDefault(x => x.MainWindowHandle == topWindowUnderCursor.Properties.NativeWindowHandle);
					}
					catch {
						// Ignore exceptions when getting bounding rectangle
					}
				}
			}

			if (wParam == WM_LBUTTONUP) {
				_topWindowOverlay?.Dispose();
				_topWindowUnderCursor = null;

				_ = windowProcessId != (uint)Environment.ProcessId ? tcs.TrySetResult(root) : tcs.TrySetResult(0);
			}
			return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
		};

		try {
			_mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty), 0);
		}
		catch {
			// If hook fails, set result zero
			_ = tcs.TrySetResult(0);
		}

		if (ct.CanBeCanceled) {
			_ = ct.Register(() => {
				_ = tcs.TrySetCanceled();

				if (_mouseHook != 0) {
					_ = UnhookWindowsHookEx(_mouseHook);
					_mouseHook = 0;
				}
			});
		}

		return tcs.Task.ContinueWith(t => {
			if (_mouseHook != 0) {
				_ = UnhookWindowsHookEx(_mouseHook);
				_mouseHook = 0;
			}
			return t.IsCompletedSuccessfully ? t.Result : 0;
		},
									 TaskScheduler.Default);
	}

	public AutomationElement? GetTopWindowUnderCursor() => GetCursorPos(out var pt)
		&& WindowFromPoint(pt) is nint hwnd and not 0
		&& GetAncestor(hwnd, GaRoot) is nint rootHwnd and not 0
			? (_defaultAutomation?.FromHandle(rootHwnd))
			: null;

	public async Task Init() {
		IsBusy = true;
		await Task.Delay(100); // Simulate some loading time;
		var currentProcessId = Environment.ProcessId;
		IEnumerable<ProcessWindowInfo> collection = [.. GetChildren(_defaultAutomation.GetDesktop())
													.Where(x => !string.IsNullOrEmpty(x.Name))
													.Where(x => x.Properties.ProcessId != currentProcessId)
													.Select(x => new ProcessWindowInfo(x.Properties.ProcessId.Value,
																					   x.Name,
																					   x.Properties.NativeWindowHandle.Value))];
		Processes = new ObservableCollection<ProcessWindowInfo>(collection);
		IsBusy = false;
		return;

		AutomationElement[] GetChildren(AutomationElement el) => IsWindowedOnly ? el.FindAllChildren(x => x.ByControlType(ControlType.Window)) : el.FindAllChildren();
	}

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetCursorPos(out POINT lpPoint);

	[LibraryImport("user32.dll")]
	private static partial IntPtr WindowFromPoint(POINT point);

	[LibraryImport("user32.dll")]
	private static partial IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

	[LibraryImport("user32.dll")]
	private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[LibraryImport("user32.dll", SetLastError = true)]
	private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool UnhookWindowsHookEx(IntPtr hhk);

	[LibraryImport("user32.dll")]
	private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr GetModuleHandle(string lpModuleName);

	private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT {
		public int x;
		public int y;
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposedValue)
			return;

		if (disposing) {
			_defaultAutomation.Dispose();
			_topWindowOverlay?.Dispose();
			_topWindowOverlay = null;
		}
		_disposedValue = true;
	}

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}

public record ProcessWindowInfo(int ProcessId, string WindowTitle, IntPtr MainWindowHandle);