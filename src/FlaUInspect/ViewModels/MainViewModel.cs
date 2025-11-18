using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;
using FlaUI.UIA3;
using FlaUInspect.Core;
using FlaUInspect.Core.Logger;
using FlaUInspect.Models;
using FlaUInspect.Views;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace FlaUInspect.ViewModels;

public class MainViewModel : ObservableObject {
    private bool _isPickingWindow;
    private MouseEventHandler? _globalMouseMoveHandler;
    
    private const int WhMouseLl = 14;
    private const uint GaRoot = 2;
    private readonly object _itemsLock = new ();
    private string? _windowHandle;
    private readonly InternalLogger? _logger;
    private AutomationBase? _automation;
    private RelayCommand? _captureSelectedItemCommand;
    private RelayCommand? _closeInfoCommand;
    private RelayCommand? _copyDetailsToClipboardCommand;
    private RelayCommand? _currentElementSaveStateCommand;
    private ObservableCollection<ElementPatternItem>? _elementPatterns = [];
    private FocusTrackingMode? _focusTrackingMode;
    private HoverMode? _hoverMode;
    private RelayCommand? _infoCommand;
    private RelayCommand? _openErrorListCommand;
    private PatternItemsFactory? _patternItemsFactory;
    private RelayCommand? _refreshCommand;
    private RelayCommand? _refreshItemCommand;
    private AutomationElement? _rootElement;
    private RelayCommand? _startNewInstanceCommand;
    private ITreeWalker? _treeWalker;
    private RelayCommand? _expandAllTreeItems;
    private RelayCommand? _recordStartCommand;
    private RelayCommand? _recordStopCommand;
    
    private static LowLevelMouseProc? _mouseProc;
    private static IntPtr _mouseHook = IntPtr.Zero;

    public MainViewModel(AutomationType automationType, string applicationVersion, string? windowHandle, InternalLogger logger) {
        _windowHandle = windowHandle;
        _logger = logger;
        ApplicationVersion = applicationVersion;
        _logger.LogEvent += (_, _) => {
            Application.Current.Dispatcher.Invoke(() => ErrorCount = _logger.Messages.Count);
        };

        SelectedAutomationType = automationType;
        Elements = [];
        BindingOperations.EnableCollectionSynchronization(Elements, _itemsLock);

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
                if (value) {
                    _hoverMode?.Start();
                } else {
                    _hoverMode?.Stop();
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

    private void ClearChildrenRecursively(ElementViewModel element) {
        foreach (ElementViewModel child in element.Children.Where(x => x != null)!) {
            ClearChildrenRecursively(child!);
        }
        element.ClearEvents();
    }

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

    private Task _recordingTask = Task.CompletedTask;
    private CancellationTokenSource _recordingCts = new ();
    private Dictionary<TimeSpan, AutomationElement[]> _records = new ();
    private RelayCommand? _pickWindowCommand;

    public ICommand RecordStartCommand => _recordStartCommand ??= new RelayCommand(_ => {
        _recordingTask.Dispose();
        _recordingCts = new CancellationTokenSource();
        _recordingTask = Task.Run(async () => await Recording(_recordingCts.Token));
    });

    public ICommand RecordStopCommand => _recordStopCommand ??= new RelayCommand(_ => {
        _recordingCts.Cancel();
    });

    public ICommand PickWindowCommand => _pickWindowCommand ??= new RelayCommand(async _ => {
        using CancellationTokenSource cts = new (TimeSpan.FromSeconds(30));
        string hwnd = await PickWindowAsync(cts.Token);
        if (hwnd != IntPtr.Zero.ToString()) {
                _windowHandle = hwnd;
                Initialize();
        }
    });

    private async Task Recording(CancellationToken token) {
        AutomationElement? desktop = _automation.GetDesktop();
        _records = new ();

        if (!string.IsNullOrEmpty(_windowHandle)) {
            desktop = _automation.FromHandle(IntPtr.Parse(_windowHandle));
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!token.IsCancellationRequested) {
            AutomationElement[] allDescendants = desktop.FindAllDescendants();

            // Check if the last record is equal to the current allDescendants
            if (_records.Count > 0) {
                var last = _records.Last();

                if (last.Value.SequenceEqual(allDescendants)) {
                    _records.Remove(last.Key);
                }
            }
            _records.Add(stopwatch.Elapsed, allDescendants);
            //_records.Add(stopwatch.Elapsed, descendants);


            //await Task.Delay(100);
        }

        Dictionary<TimeSpan, List<UiNode>> elementsMap = new ();

        try {
            foreach (KeyValuePair<TimeSpan, AutomationElement[]> keyValuePair in _records) {
                List<UiNode> descendants = keyValuePair.Value
                                                       .Select(x => new UiNode {
                                                           AutomationElement = x,
                                                           ControlType = x.ControlType,
                                                           Name = x.Name,
                                                           Parent = x.Parent
                                                       }).ToList();
                List<UiNode> roots = BuildTree(descendants);
                elementsMap.Add(keyValuePair.Key, roots);
            }

            for (int i = 0; i < elementsMap.Count; i++) {
                List<UiNode> elements = elementsMap.ElementAt(i).Value;
                string json = ExportToJson(elements);
                await File.WriteAllTextAsync($"c:\\tmp\\FUI\\recording-{i:D2}.json", json);
            }

        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }

    private string ExportToJson(List<UiNode> nodes) {
        List<UiNodeDto> dtoList = nodes
                                  .Select(UiNodeMapper.ToDto)
                                  .ToList();
        
        return JsonSerializer.Serialize(
            dtoList,
            new JsonSerializerOptions { WriteIndented = true }
        );
        
        
    }
    // private string ExportToJson(List<UiNode> elementsMap) {
    //     System.Text.StringBuilder sb = new ();
    //     using JsonWriter writer = new (sb);
    //
    //     void WriteNode(UiNode node) {
    //         writer.WriteStartObject();
    //         writer.WriteProperty("Name", node.Name);
    //         writer.WriteProperty("ControlType", node.ControlType.ToString());
    //
    //         if (node.Children.Any()) {
    //             writer.WritePropertyName("Children");
    //             writer.WriteStartArray();
    //
    //             foreach (UiNode child in node.Children) {
    //                 WriteNode(child);
    //             }
    //
    //             writer.WriteEndArray();
    //         }
    //
    //         writer.WriteEndObject();
    //     }
    //
    //     writer.WriteStartArray();
    //
    //     foreach (UiNode node in elementsMap) {
    //         WriteNode(node);
    //     }
    //
    //     writer.WriteEndArray();
    //     return sb.ToString();
    // }

    class UiNode {
        public ControlType ControlType { get; set; }
        public string Name { get; set; }
        public AutomationElement? Parent { get; set; }
        public List<UiNode> Children { get; set; } = new ();
        public AutomationElement AutomationElement { get; set; }

        public override string ToString() {
            return $"{Name}[{ControlType}]";
        }
    }
    
    public class UiNodeDto
    {
        public string ControlType { get; set; }
        public string Name { get; set; }
        public List<UiNodeDto> Children { get; set; } = new();
    }
    
    static class UiNodeMapper
    {
        public static UiNodeDto ToDto(UiNode node)
        {
            return new UiNodeDto
            {
                ControlType = node.ControlType.ToString(),
                Name = node.Name,
                Children = node.Children.Select(ToDto).ToList()
            };
        }
    }

    // Build tree from flat list based on ParentAutomationId
    List<UiNode> BuildTree(List<UiNode> nodes) {
        Dictionary<AutomationElement, UiNode> lookup = nodes.ToDictionary(n => n.AutomationElement, new AutomationElementEqualityComparer());
        List<UiNode> roots = new ();

        foreach (UiNode node in nodes) {
            UiNode? parent = lookup.Values.FirstOrDefault(x => x.AutomationElement.Equals(node.Parent));

            // var iii = node.Parent.GetHashCode();
            // bool tryGetValue = lookup.TryGetValue(node.Parent, out UiNode? parent);
            //
            // if (node.Parent.Name != applicationName && tryGetValue) {
            
            //if (node.Parent.Name != applicationName && parent != null) {
            if (parent != null) {
                parent.Children.Add(node);
            } else {
                roots.Add(node);
            }
        }
        return roots;
    }

    class AutomationElementEqualityComparer : IEqualityComparer<AutomationElement> {
        public bool Equals(AutomationElement? x, AutomationElement? y) {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.Equals(y);
        }

        public int GetHashCode(AutomationElement obj) {
            return obj?.GetHashCode() ?? 0;
        }
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
        
        _automation = (SelectedAutomationType == AutomationType.UIA2 ? (AutomationBase?)new UIA2Automation() : new UIA3Automation()) ?? new UIA3Automation();
        _patternItemsFactory = new PatternItemsFactory(_automation);
        if (string.IsNullOrEmpty(_windowHandle))
            _rootElement = _automation.GetDesktop();
        else {
            _rootElement = _automation.FromHandle(IntPtr.Parse(_windowHandle));
        }

        // if (!string.IsNullOrEmpty(_windowHandle)) {
        //     //_rootElement = _rootElement?.FindFirstChild(cf => cf.ByName(_windowHandle)) ?? _rootElement;
        //     _rootElement = _rootElement?.FindFirstChild(cf => cf..ByName(_windowHandle)) ?? _rootElement;
        // }
        ElementViewModel desktopViewModel = new (_rootElement, null, _logger);

        desktopViewModel.SelectionChanged += obj => {
            SelectedItem = obj;
        };
        desktopViewModel.LoadChildren(string.IsNullOrEmpty(_windowHandle) ? 0 : 1000);

        // lock (_itemsLock) {
        //     Elements.Add(desktopViewModel);
        //     desktopViewModel.IsExpanded = true;
        // }

        // if (string.IsNullOrEmpty(_windowHandle)) {
        //     Elements.Add(desktopViewModel);
        //     desktopViewModel.IsExpanded = true;
        // } else {
        //     foreach (ElementViewModel child in desktopViewModel.Children) {
        //         Elements.Add(child);
        //     }
        // }
        
        foreach (ElementViewModel child in desktopViewModel.Children) {
            Elements.Add(child);
        }

        // Initialize TreeWalker
        _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

        // Initialize hover
        EnableHoverMode = false;
        _hoverMode?.Stop();
        _hoverMode?.Dispose();
        _hoverMode = new HoverMode(_automation, _logger);
        _hoverMode.ElementHovered += ElementToSelectChanged;

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

        // Expand the root element if needed
        
        //var expandedElements = FindElement(Elements);
        
        // if (!Elements[0].IsExpanded) {
        //     Elements[0].IsExpanded = true;
        // }

        //ElementViewModel elementVm = Elements[0];

        IEnumerable<ElementViewModel> viewModels = Elements;
        ElementViewModel? nextElementVm = null;
        Stack<ElementViewModel> sss = new ();
        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            
            //ElementViewModel? nextElementVm = FindElement(elementVm, elementOnPath);
            nextElementVm = FindElement(viewModels, elementOnPath);
            sss.Push(nextElementVm);
            // if (nextElementVm == null) {
            //     // Could not find next element, try reloading the parent
            //     elementVm.LoadChildren(0);
            //     // Now search again
            //     nextElementVm = FindElement(viewModels, elementOnPath);
            //
            //     if (nextElementVm == null) {
            //         // The next element is still not found, exit the loop
            //         _logger?.LogError("Could not find the next element!");
            //         break;
            //     }
            // }
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

        var previousCursor = Mouse.OverrideCursor;
        var mainWindow = Application.Current?.MainWindow;
        Mouse.OverrideCursor = Cursors.Cross;


        try {
            IntPtr hwnd = await WaitForMouseClickWindowAsync(ct);

            if (hwnd == IntPtr.Zero) {
                return IntPtr.Zero.ToString();
            }

            return hwnd.ToString();

            // GetWindowThreadProcessId(hwnd, out uint pid);
            //
            // return pid;
            // if (pid == 0) {
            //     return;
            // }

            // Process proc;
            //
            // try {
            //     proc = Process.GetProcessById((int)pid);
            // }
            // catch {
            //     return;
            // }

            // FilterText = proc.Id.ToString();
            // SelectedProcess = new ProcessItem(proc.ProcessName, proc.Id);
            // await ReadProcessWindowsAsync(SelectedProcess);
        }
        catch (OperationCanceledException) {
            // canceled - ignore
        }
        finally {
            Application.Current.Dispatcher.Invoke(() => Mouse.OverrideCursor = previousCursor);
            if (mainWindow != null) {
                 //mainWindow.Visibility = Visibility.Visible;
            }
            
        }
        return IntPtr.Zero.ToString();
    }

    private AutomationElement? _topWindowUnderCursor;
    private ElementOverlay? _topWindowOverlay;
    
    // Waits for a mouse click and returns the top-level window handle at click point
    private Task<IntPtr> WaitForMouseClickWindowAsync(CancellationToken ct) {
        var tcs = new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);

        _mouseProc = (nCode, wParam, lParam) => {
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;

            // if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONUP) {
            //     if (GetCursorPos(out var pt)) {
            //         IntPtr hwnd = WindowFromPoint(pt);
            //         IntPtr root = GetAncestor(hwnd, GaRoot);
            //         tcs.TrySetResult(root);
            //     }
            // }
            
           if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)0x0200)) {
                if (GetCursorPos(out var pt)) {
                    IntPtr hwnd = WindowFromPoint(pt);
                    IntPtr root = GetAncestor(hwnd, GaRoot);
            
                    // Highlight the window under the mouse
                    AutomationElement? topWindowUnderCursor = GetTopWindowUnderCursor();

                    if (_topWindowUnderCursor == null || !_topWindowUnderCursor.Equals(topWindowUnderCursor)) {
                        _topWindowOverlay?.Dispose();

                        try {
                            Rectangle boundingRectangleValue = topWindowUnderCursor.Properties.BoundingRectangle.Value;
                            Color yellow = Color.FromArgb((int)(255 * 0.05), Color.Yellow);
                            
                            _topWindowOverlay = new ElementOverlay(yellow);
                            _topWindowOverlay.CreateAndShowForms(boundingRectangleValue);
                            _topWindowUnderCursor = topWindowUnderCursor;
                        } catch {
                            // Ignore exceptions when getting bounding rectangle
                        }
                    }
                    //ElementHighlighter.HighlightElement(topWindowUnderCursor, _logger);
            
                    if (wParam == (IntPtr)WM_LBUTTONUP) {
                        _topWindowOverlay?.Dispose();
                        _topWindowUnderCursor = null;
                        tcs.TrySetResult(root);
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        };

        try {
            IntPtr hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc!, hMod, 0);
        }
        catch {
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
    
    public AutomationElement? GetTopWindowUnderCursor()
    {
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