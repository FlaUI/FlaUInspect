using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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

public class StartupViewModel : ObservableObject {
    private const int WhMouseLl = 14;
    private const uint GaRoot = 2;

    private static IntPtr _mouseHook = IntPtr.Zero;
    private static LowLevelMouseProc? _mouseProc;

    private readonly AutomationBase _defaultAutomation = new UIA3Automation();

    private ICollectionView _filteredProcesses;

    private ObservableCollection<ProcessWindowInfo> _processes = [];
    private ElementOverlay? _topWindowOverlay;
    private AutomationElement? _topWindowUnderCursor;

    public StartupViewModel() {
        IsWindowedOnly = true;
        RefreshCommand = new AsyncRelayCommand(async () => {
            await Init();
        });

        PickCommand = new AsyncRelayCommand(async () => {
            using CancellationTokenSource cts = new (TimeSpan.FromSeconds(30));
            IntPtr hwnd = await PickWindowAsync(cts.Token);
            SelectedProcess = Processes.FirstOrDefault(x => x.MainWindowHandle == hwnd);
        });

        SettingCommand = new RelayCommand(_ => {
            ISettingsService<FlaUiAppSettings>? settingsService = App.Services.GetService<ISettingsService<FlaUiAppSettings>>();
            FlaUiAppSettings flaUiAppSettings = settingsService.Load();
            Editable<FlaUiAppSettings> settings = new (flaUiAppSettings,
                                                       s => s.Clone() as FlaUiAppSettings,
                                                       (from, to) => from.CopyTo(to),
                                                       (a, b) => a.Equals(b));

            DialogContent = new SettingsViewModel();
        });

        CloseSettingCommand = new RelayCommand(_ => {
                                                   if (DialogContent is IDialogViewModel { CanClose: true } closableViewModel) {
                                                       closableViewModel.Close();
                                                       DialogContent = null;
                                                   }
                                               },
                                               _ => DialogContent is IDialogViewModel { CanClose: true });

        SaveSettingCommand = new RelayCommand(_ => {
                                                  if (DialogContent is IDialogViewModel { CanClose: true } closableViewModel) {
                                                      ISettingViewModel? settingsViewModel = DialogContent as ISettingViewModel;
                                                      closableViewModel.Save();
                                                      DialogContent = null;

                                                      if (settingsViewModel != null) {
                                                          App.ApplyAppOption(settingsViewModel.Settings.Current);
                                                      }
                                                  }
                                              },
                                              _ => DialogContent is IDialogViewModel { CanClose: true });

        _filteredProcesses = CollectionViewSource.GetDefaultView(_processes);
        _filteredProcesses.Filter = FilterProcesses;

        DialogContent = null;
    }


    public object? DialogContent {
        get => GetProperty<object?>();
        set => SetProperty(value);
    }

    public ICollectionView FilteredProcesses => _filteredProcesses;

    public ICommand SettingCommand { get; private set; }
    public ICommand RefreshCommand { get; private set; }
    public ICommand PickCommand { get; }

    public ICommand CloseSettingCommand { get; }
    public ICommand SaveSettingCommand { get; }


    public bool IsBusy {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public ProcessWindowInfo? SelectedProcess {
        get => GetProperty<ProcessWindowInfo>();
        set => SetProperty(value);
    }

    public ObservableCollection<ProcessWindowInfo> Processes {
        get => _processes;
        private set {
            SetProperty(ref _processes, value);
            _filteredProcesses = CollectionViewSource.GetDefaultView(_processes);
            _filteredProcesses.Filter = FilterProcesses;
            OnPropertyChanged(nameof(FilteredProcesses));
        }
    }

    public string? FilterProcess {
        get => GetProperty<string?>();
        set {
            if (SetProperty(value)) {
                _filteredProcesses?.Refresh();
            }
        }
    }

    public bool IsWindowedOnly {
        get => GetProperty<bool>();
        set {
            if (SetProperty(value)) {
                Task.Run(async () => await Init());
            }
        }
    }

    private bool FilterProcesses(object obj) {
        if (obj is not ProcessWindowInfo p) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FilterProcess)) {
            return true;
        }

        return p.WindowTitle.Contains(FilterProcess, StringComparison.OrdinalIgnoreCase)
               || p.ProcessId.ToString().Contains(FilterProcess, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IntPtr> PickWindowAsync(CancellationToken ctsToken) {
        Cursor? previousCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Cross;

        try {
            return await WaitForMouseClickWindowAsync(ctsToken);
        } catch (OperationCanceledException) {
            // canceled - ignore
        } finally {
            Application.Current.Dispatcher.Invoke(() => Mouse.OverrideCursor = previousCursor);
        }
        return IntPtr.Zero;
    }

    private Task<IntPtr> WaitForMouseClickWindowAsync(CancellationToken ct) {
        TaskCompletionSource<IntPtr> tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);

        _mouseProc = (nCode, wParam, lParam) => {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;

            if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)0x0200)) {
                if (GetCursorPos(out POINT pt)) {
                    IntPtr hwnd = WindowFromPoint(pt);
                    IntPtr root = GetAncestor(hwnd, GaRoot);

                    // Highlight the window under the mouse, but skip if it is the current process
                    GetWindowThreadProcessId(root, out uint windowProcessId);

                    if (windowProcessId != (uint)Process.GetCurrentProcess().Id) {
                        AutomationElement? topWindowUnderCursor = GetTopWindowUnderCursor();

                        if (_topWindowUnderCursor == null || !_topWindowUnderCursor.Equals(topWindowUnderCursor)) {
                            _topWindowOverlay?.Dispose();

                            try {
                                Rectangle boundingRectangleValue = topWindowUnderCursor.Properties.BoundingRectangle.Value;
                                _topWindowOverlay = App.FlaUiAppOptions.PickOverlay();
                                _topWindowOverlay?.Show(boundingRectangleValue);
                                _topWindowUnderCursor = topWindowUnderCursor;

                                SelectedProcess = Processes.FirstOrDefault(x => x.MainWindowHandle == topWindowUnderCursor.Properties.NativeWindowHandle);
                            } catch {
                                // Ignore exceptions when getting bounding rectangle
                            }
                        }

                    } else {
                        _topWindowOverlay?.Dispose();
                        _topWindowUnderCursor = null;
                    }

                    if (wParam == (IntPtr)WM_LBUTTONUP) {
                        _topWindowOverlay?.Dispose();
                        _topWindowUnderCursor = null;

                        if (windowProcessId != (uint)Process.GetCurrentProcess().Id) {
                            tcs.TrySetResult(root);
                        } else {
                            tcs.TrySetResult(IntPtr.Zero);
                        }
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        };

        try {
            IntPtr hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc!, hMod, 0);
        } catch {
            // If hook fails, set result zero
            tcs.TrySetResult(IntPtr.Zero);
        }

        if (ct.CanBeCanceled) {
            ct.Register(() => {
                tcs.TrySetCanceled();

                if (_mouseHook != IntPtr.Zero) {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
            });
        }

        return tcs.Task.ContinueWith(t => {
                                         if (_mouseHook != IntPtr.Zero) {
                                             UnhookWindowsHookEx(_mouseHook);
                                             _mouseHook = IntPtr.Zero;
                                         }
                                         return t.IsCompletedSuccessfully ? t.Result : IntPtr.Zero;
                                     },
                                     TaskScheduler.Default);
    }

    public AutomationElement? GetTopWindowUnderCursor() {
        if (!GetCursorPos(out POINT pt))
            return null;

        IntPtr hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
            return null;

        IntPtr rootHwnd = GetAncestor(hwnd, GaRoot);
        if (rootHwnd == IntPtr.Zero)
            return null;

        return _defaultAutomation?.FromHandle(rootHwnd);
    }

    public async Task Init() {
        IsBusy = true;
        await Task.Delay(100); // Simulate some loading time;
        int currentProcessId = Environment.ProcessId;
        IEnumerable<ProcessWindowInfo> collection = GetChildren(_defaultAutomation.GetDesktop())
                                                    .Where(x => !string.IsNullOrEmpty(x.Name))
                                                    .Where(x => x.Properties.ProcessId != currentProcessId)
                                                    .Select(x => new ProcessWindowInfo(x.Properties.ProcessId.Value,
                                                                                       x.Name,
                                                                                       x.Properties.NativeWindowHandle.Value))
                                                    .ToList();
        Processes = new ObservableCollection<ProcessWindowInfo>(collection);
        IsBusy = false;
        return;

        AutomationElement[] GetChildren(AutomationElement el) {
            return IsWindowedOnly ? el.FindAllChildren(x => x.ByControlType(ControlType.Window)) : el.FindAllChildren();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT {
        public int X;
        public int Y;
    }
}

public record ProcessWindowInfo(int ProcessId, string WindowTitle, IntPtr MainWindowHandle);