using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using FlaUI.UIA2;
using FlaUI.UIA3;
using FlaUInspect.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.Models;
using FlaUInspect.Views;
using Microsoft.Win32;
using Application = System.Windows.Application;
using Window = System.Windows.Window;
// ReSharper disable InconsistentNaming

namespace FlaUInspect.ViewModels;

public class MainViewModel : ObservableObject {
    private const int WhMouseLl = 14;
    private const uint GaRoot = 2;

    private static LowLevelMouseProc? _mouseProc;
    private static IntPtr _mouseHook = IntPtr.Zero;
    private readonly AutomationBase? _automation;
    private readonly HoverMouseMode? _hoverMouseMode;
    private readonly object _itemsLock = new ();
    private readonly InternalLogger? _logger;
    private string _applicationName = string.Empty;
    private RelayCommand? _captureSelectedItemCommand;
    private RelayCommand? _closeInfoCommand;
    private RelayCommand? _copyDetailsToClipboardCommand;
    private RelayCommand? _currentElementSaveStateCommand;
    private ObservableCollection<ElementPatternItem>? _elementPatterns = [];
    private RelayCommand? _expandAllTreeItems;
    private FocusTrackingMode? _focusTrackingMode;
    private RelayCommand? _infoCommand;
    private RelayCommand? _openErrorListCommand;
    private PatternItemsFactory? _patternItemsFactory;


    private RelayCommand? _pickWindowCommand;
    private RelayCommand? _refreshCommand;
    private RelayCommand? _refreshItemCommand;
    private AutomationElement? _rootElement;
    private RelayCommand? _startNewInstanceCommand;
    private ElementOverlay? _topWindowOverlay;

    private AutomationElement? _topWindowUnderCursor;
    private ITreeWalker? _treeWalker;
    private string? _windowHandle;

    public MainViewModel(AutomationType automationType, string applicationVersion, string? windowHandle, InternalLogger logger) {
        _windowHandle = windowHandle;
        _logger = logger;
        ApplicationVersion = applicationVersion;
        _logger.LogEvent += (_, _) => {
            Application.Current.Dispatcher.Invoke(() => ErrorCount = _logger.Messages.Count);
        };

        SelectedAutomationType = automationType;
        _automation = (SelectedAutomationType == AutomationType.UIA2 ? (AutomationBase?)new UIA2Automation() : new UIA3Automation()) ?? new UIA3Automation();

        Elements = [];
        BindingOperations.EnableCollectionSynchronization(Elements, _itemsLock);
        _hoverMouseMode = new HoverMouseMode(_automation);

        GlobalMouseHook.MouseMove += (x, y) => {
            GetCursorPos(out POINT pt);
            IntPtr hwnd = WindowFromPoint(pt);
            IntPtr root = GetAncestor(hwnd, GaRoot);

            if (!string.IsNullOrEmpty(_windowHandle)) {
                var windowHandle = IntPtr.Parse(_windowHandle);

                if (root == windowHandle) {
                    _hoverMouseMode.Refresh();
                }
            }
        };

        GlobalMouseHook.Start();
    }


    public ICommand OpenErrorListCommand =>
        _openErrorListCommand ??= new RelayCommand(_ => {
                                                       if (_logger is { Messages.IsEmpty: false }) {
                                                           ErrorListWindow errorListWindow = new (_logger);
                                                           errorListWindow.ShowDialog();
                                                       }
                                                   },
                                                   _ => !_logger?.Messages.IsEmpty ?? false);

    public int ErrorCount {
        get => GetProperty<int>();
        private set => SetProperty(value);
    }

    public bool EnableHoverMode {
        get => GetProperty<bool>();
        set {
            if (SetProperty(value)) {
                if (_hoverMouseMode != null) {
                    _hoverMouseMode.IsEnabled = value;
                }
            }
        }
    }

    public bool EnableHighLightSelectionMode {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public bool EnableFocusTrackingMode {
        get => GetProperty<bool>();
        set {
            if (SetProperty(value)) {
                if (value) {
                    _focusTrackingMode?.Start();
                } else {
                    _focusTrackingMode?.Stop();
                }
            }
        }
    }

    public bool EnableXPath {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public AutomationType SelectedAutomationType {
        get => GetProperty<AutomationType>();
        private init => SetProperty(value);
    }

    public ObservableCollection<ElementViewModel> Elements { get; private set; }

    public ICommand StartNewInstanceCommand =>
        _startNewInstanceCommand ??= new RelayCommand(_ => {
            ProcessStartInfo info = new (Assembly.GetExecutingAssembly().Location);
            Process.Start(info);
        });

    public ICommand CaptureSelectedItemCommand =>
        _captureSelectedItemCommand ??= new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }
            Bitmap capturedImage = SelectedItem.AutomationElement.Capture();
            SaveFileDialog saveDialog = new () {
                Filter = "Png file (*.png)|*.png"
            };

            if (saveDialog.ShowDialog() == true) {
                capturedImage.Save(saveDialog.FileName, ImageFormat.Png);
            }
            capturedImage.Dispose();
        });

    public ICommand RefreshCommand =>
        _refreshCommand ??= new RelayCommand(_ => {
            EnableHoverMode = false;
            EnableFocusTrackingMode = false;
            EnableHighLightSelectionMode = false;
            Initialize();
        });

    public ElementViewModel? SelectedItem {
        get => GetProperty<ElementViewModel>();
        set {
            if (SetProperty(value)) {
                if (value != null) {
                    ReadPatternsForSelectedItem(value.AutomationElement);
                }
            }
        }
    }

    public ICommand RefreshItemCommand =>
        _refreshItemCommand ??= new RelayCommand(o => {
            if (o is ElementViewModel item) {
                item.Children.Clear();
                item.IsExpanded = true;
            }
        });

    public IEnumerable<ElementPatternItem> ElementPatterns {
        get => _elementPatterns ?? Enumerable.Empty<ElementPatternItem>();
        private set => SetProperty(ref _elementPatterns, value as ObservableCollection<ElementPatternItem>);
    }

    public ICommand InfoCommand => _infoCommand ??= new RelayCommand(_ => {
        IsInfoVisible = !IsInfoVisible;
    });

    public bool IsInfoVisible {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }
    public string? ApplicationVersion {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public ICommand CloseInfoCommand => _closeInfoCommand ??= new RelayCommand(_ => {
        IsInfoVisible = false;
    });

    public ICommand CurrentElementSaveStateCommand => _currentElementSaveStateCommand ??= new RelayCommand(_ => {
        if (SelectedItem?.AutomationElement == null) {
            return;
        }

        try {
            XDocument document = new ();
            document.Add(new XElement("Root"));
            ExportElement(document.Root!, SelectedItem);

            Clipboard.SetText(document.ToString());
            CopiedNotificationCurrentElementSaveStateRequested?.Invoke();
        } catch (Exception e) {
            _logger?.LogError(e.ToString());
        }
    });

    public ICommand CollapseAllDetailsCommand => new RelayCommand(_ => {
        foreach (ElementPatternItem pattern in ElementPatterns) {
            pattern.IsExpanded = false;
        }
    });

    public ICommand ExpandAllDetailsCommand => new RelayCommand(_ => {
        foreach (ElementPatternItem pattern in ElementPatterns) {
            pattern.IsExpanded = true;
        }
    });

    public ICommand CopyDetailsToClipboardCommand => _copyDetailsToClipboardCommand ??= new RelayCommand(_ => {
        if (SelectedItem?.AutomationElement == null) {
            return;
        }

        try {
            XDocument document = new ();
            document.Add(new XElement("Root"));

            foreach (ElementPatternItem elementPatternItem in ElementPatterns) {
                XElement patternNode = new ("Pattern",
                                            new XAttribute("Name", elementPatternItem.PatternName),
                                            new XAttribute("Id", elementPatternItem.PatternIdName));

                foreach (PatternItem patternItem in elementPatternItem.Children) {
                    XElement itemNode = new ("Item",
                                             new XAttribute("Key", patternItem.Key),
                                             new XAttribute("Value", patternItem.Value ?? string.Empty));
                    patternNode.Add(itemNode);
                }

                if (patternNode.HasElements) {
                    document.Root!.Add(patternNode);
                }
            }
            Clipboard.SetText(document.ToString());
            CopiedNotificationRequested?.Invoke();
        } catch (Exception e) {
            _logger?.LogError(e.ToString());
        }
    });

    public ICommand ExpandAllTreeItems => _expandAllTreeItems ??= new RelayCommand(_ => {
        foreach (ElementViewModel element in Elements) {
            element.IsExpanded = true;
            element.ExpandAll();
        }
    });


    public ICommand PickWindowCommand => _pickWindowCommand ??= new RelayCommand(async _ => {
        using CancellationTokenSource cts = new (TimeSpan.FromSeconds(30));
        string hwnd = await PickWindowAsync(cts.Token);

        if (hwnd != IntPtr.Zero.ToString()) {
            _windowHandle = hwnd;
            ApplicationName = _automation?.FromHandle(IntPtr.Parse(_windowHandle))?.Name ?? string.Empty;
            Initialize();
        }
    });

    public string ApplicationName {
        get => _applicationName;
        private set => SetProperty(ref _applicationName, value);
    }

    private void ClearChildrenRecursively(ElementViewModel element) {
        foreach (ElementViewModel child in element.Children.Where(x => x != null)!) {
            ClearChildrenRecursively(child!);
        }
        element.ClearEvents();
    }


    public event Action? CopiedNotificationRequested;
    public event Action? CopiedNotificationCurrentElementSaveStateRequested;

    private void ExportElement(XElement parent, ElementViewModel element) {
        XElement xElement = CreateXElement(element);
        parent.Add(xElement);

        try {
            foreach (ElementViewModel children in element.Children.Where(x => x is { IsExpanded: true }).Where(x => x != null)!) {
                try {
                    ExportElement(xElement, children!);
                } catch {
                    // ignored
                }
            }
        } catch {
            // ignored
        }
    }

    private XElement CreateXElement(ElementViewModel element) {

        List<XAttribute> attrs = [
            new ("Name", element.Name),
            new ("AutomationId", element.AutomationId),
            new ("ControlType", element.ControlType)
        ];

        if (EnableXPath) {
            attrs.Add(new XAttribute("XPath", element.XPath));
        }

        XElement xElement = new ("Element", attrs);
        return xElement;
    }

    private void ReadPatternsForSelectedItem(AutomationElement? selectedItemAutomationElement) {
        if (SelectedItem?.AutomationElement == null || selectedItemAutomationElement == null) {
            return;
        }

        if (_patternItemsFactory == null) {
            return;
        }

        if (EnableHighLightSelectionMode) {
            ElementHighlighter.HighlightElement(SelectedItem.AutomationElement, _logger);
        }

        try {
            HashSet<PatternId> supportedPatterns = [.. selectedItemAutomationElement.GetSupportedPatterns()];
            IDictionary<string, PatternItem[]> patternItemsForElement = _patternItemsFactory.CreatePatternItemsForElement(selectedItemAutomationElement, supportedPatterns);

            foreach (ElementPatternItem elementPattern in ElementPatterns) {
                elementPattern.IsVisible = elementPattern.PatternIdName == PatternItemsFactory.Identification
                                           || elementPattern.PatternIdName == PatternItemsFactory.Details
                                           || elementPattern.PatternIdName == PatternItemsFactory.PatternSupport
                                           || supportedPatterns.Any(x => x.Name.Equals(elementPattern.PatternIdName));
                elementPattern.Children.Clear();

                if (patternItemsForElement.TryGetValue(elementPattern.PatternIdName, out PatternItem[]? children)) {
                    foreach (PatternItem patternItem in children) {
                        elementPattern.Children.Add(patternItem);
                    }
                }

                if (!elementPattern.Children.Any()) {
                    elementPattern.IsVisible = false;
                }
            }
        } catch (Exception e) {
            _logger?.LogError(e.ToString());
        }
    }

    public void Initialize() {
        foreach (ElementViewModel element in Elements) {
            ClearChildrenRecursively(element);
        }
        Elements.Clear();

        _patternItemsFactory = new PatternItemsFactory(_automation);

        if (string.IsNullOrEmpty(_windowHandle)) {
            _rootElement = _automation.GetDesktop();
        } else {
            _rootElement = _automation.FromHandle(IntPtr.Parse(_windowHandle));
        }

        ElementViewModel desktopViewModel = new (_rootElement, null, _logger);

        desktopViewModel.SelectionChanged += obj => {
            SelectedItem = obj;
        };
        desktopViewModel.LoadChildren(string.IsNullOrEmpty(_windowHandle) ? 0 : 1000);

        foreach (ElementViewModel child in desktopViewModel.Children) {
            Elements.Add(child);
        }

        // Initialize TreeWalker
        _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

        // Initialize hover
        EnableHoverMode = false;

        if (_hoverMouseMode != null) {
            _hoverMouseMode.IsEnabled = false;
        }

        // Initialize focus tracking
        _focusTrackingMode = new FocusTrackingMode(_automation);
        _focusTrackingMode.ElementFocused += ElementToSelectChanged;

        ElementPatterns = GetDefaultPatternList();
        SelectedItem = Elements.Count == 0 ? null : Elements[0];

        OnPropertyChanged(nameof(Elements));
        OnPropertyChanged(nameof(ElementPatterns));
    }

    private ObservableCollection<ElementPatternItem> GetDefaultPatternList() {
        return new ObservableCollection<ElementPatternItem>(new[] {
                                                                    new ElementPatternItem("Identification", PatternItemsFactory.Identification, true, true),
                                                                    new ElementPatternItem("Details", PatternItemsFactory.Details, true, true),
                                                                    new ElementPatternItem("Pattern Support", PatternItemsFactory.PatternSupport, true, true)
                                                                }
                                                                .Concat(
                                                                    (_automation?.PatternLibrary.AllForCurrentFramework ?? [])
                                                                    .Select(x => {
                                                                        ElementPatternItem patternItem = new (x.Name, x.Name) {
                                                                            IsVisible = true
                                                                        };
                                                                        return patternItem;
                                                                    })));
    }

    private void ElementToSelectChanged(AutomationElement? obj) {
        // Build a stack from the root to the hovered item
        Stack<AutomationElement> pathToRoot = new ();

        while (obj != null) {
            // Break on circular relationship (should not happen?)
            if (pathToRoot.Contains(obj) || obj.Equals(_rootElement)) {
                break;
            }

            pathToRoot.Push(obj);

            try {
                obj = _treeWalker?.GetParent(obj);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }
        }

        IEnumerable<ElementViewModel> viewModels = Elements;
        ElementViewModel? nextElementVm = null;
        Stack<ElementViewModel> sss = new ();

        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();

            nextElementVm = FindElement(viewModels, elementOnPath);
            sss.Push(nextElementVm);

            nextElementVm.LoadChildren(0);
            viewModels = nextElementVm.Children;

            if (!nextElementVm.IsExpanded) {
                nextElementVm.IsExpanded = true;
            }
        }
        // Select the last element
        SelectedItem = nextElementVm;
        SelectedItem.IsSelected = true;
    }

    private ElementViewModel? FindElement(ElementViewModel parent, AutomationElement element) {
        return parent.Children.FirstOrDefault(child => {
            if (child?.AutomationElement == null) {
                return false;
            }

            try {
                return child.AutomationElement.Equals(element);
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }

            return false;
        });
    }

    private ElementViewModel? FindElement(IEnumerable<ElementViewModel> viewModels, AutomationElement element) {
        return viewModels.FirstOrDefault(el => {
            if (el?.AutomationElement == null) {
                return false;
            }

            try {
                return el.AutomationElement.Equals(element);
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }

            return false;
        });
    }

    private async Task<string> PickWindowAsync(CancellationToken ct) {

        Cursor? previousCursor = Mouse.OverrideCursor;
        Window? mainWindow = Application.Current?.MainWindow;
        Mouse.OverrideCursor = Cursors.Cross;


        try {
            IntPtr hwnd = await WaitForMouseClickWindowAsync(ct);

            if (hwnd == IntPtr.Zero) {
                return IntPtr.Zero.ToString();
            }

            return hwnd.ToString();
        } catch (OperationCanceledException) {
            // canceled - ignore
        } finally {
            Application.Current.Dispatcher.Invoke(() => Mouse.OverrideCursor = previousCursor);
        }
        return IntPtr.Zero.ToString();
    }

    // Waits for a mouse click and returns the top-level window handle at click point
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
                                _topWindowOverlay = new ElementOverlay(new ElementOverlayConfiguration(2, 0, Color.Red, ElementOverlay.BoundRectangleFactory));
                                _topWindowOverlay.Show(boundingRectangleValue);
                                _topWindowUnderCursor = topWindowUnderCursor;
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

        return _automation?.FromHandle(rootHwnd);
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