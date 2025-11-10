using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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

namespace FlaUInspect.ViewModels;

public class MainViewModel : ObservableObject {

    private readonly object _itemsLock = new ();
    private readonly string? _applicationName;
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

    public MainViewModel(AutomationType automationType, string applicationVersion, string? applicationName, InternalLogger logger) {
        _applicationName = applicationName;
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
            Elements.Clear();
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
    private Dictionary<TimeSpan, ElementViewModel[]> _records = new ();

    public ICommand RecordStartCommand => _recordStartCommand ??= new RelayCommand(_ => {
        _recordingTask.Dispose();
        _recordingCts = new CancellationTokenSource();
        _recordingTask = Task.Run(async () => await Recording(_recordingCts.Token));
    });

    public ICommand RecordStopCommand => _recordStopCommand ??= new RelayCommand(_ => {
        _recordingCts.Cancel();
    });
    
    class UiNode
    {
        public string ControlType { get; set; }
        public string Name { get; set; }
        public List<UiNode> Children { get; set; } = new();
    }

    private async Task Recording(CancellationToken token) {
        AutomationElement? desktop = _automation.GetDesktop();
        _records = new ();

        if (!string.IsNullOrEmpty(_applicationName)) {
            desktop = desktop?.FindFirstChild(cf => cf.ByName(_applicationName)) ?? desktop;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        while (token.IsCancellationRequested == false) {
            //AutomationElement[] elements = desktop.FindAllDescendants();
            List<ElementViewModel> elements = [];
            foreach (AutomationElement child in desktop.FindAllChildren()) {
                ElementViewModel item = new (child, null, _logger);
                item.LoadChildren(9999);
                elements.Add(item);
            }
            _records.Add(stopwatch.Elapsed, elements.ToArray());
            await Task.Delay(100);
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
        _automation = (SelectedAutomationType == AutomationType.UIA2 ? (AutomationBase?)new UIA2Automation() : new UIA3Automation()) ?? new UIA3Automation();
        _patternItemsFactory = new PatternItemsFactory(_automation);
        _rootElement = _automation.GetDesktop();

        if (!string.IsNullOrEmpty(_applicationName)) {
            _rootElement = _rootElement?.FindFirstChild(cf => cf.ByName(_applicationName)) ?? _rootElement;
        }
        ElementViewModel desktopViewModel = new (_rootElement, null, _logger);

        desktopViewModel.SelectionChanged += obj => {
            SelectedItem = obj;
        };
        desktopViewModel.LoadChildren(string.IsNullOrEmpty(_applicationName) ? 0 : 1000);

        // lock (_itemsLock) {
        //     Elements.Add(desktopViewModel);
        //     desktopViewModel.IsExpanded = true;
        // }

        if (string.IsNullOrEmpty(_applicationName)) {
            Elements.Add(desktopViewModel);
            desktopViewModel.IsExpanded = true;
        } else {
            foreach (ElementViewModel child in desktopViewModel.Children) {
                Elements.Add(child);
            }
        }

        // Initialize TreeWalker
        _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

        // Initialize hover
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
        if (!Elements[0].IsExpanded) {
            Elements[0].IsExpanded = true;
        }

        ElementViewModel elementVm = Elements[0];

        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            ElementViewModel? nextElementVm = FindElement(elementVm, elementOnPath);

            if (nextElementVm == null) {
                // Could not find next element, try reloading the parent
                elementVm.LoadChildren(0);
                // Now search again
                nextElementVm = FindElement(elementVm, elementOnPath);

                if (nextElementVm == null) {
                    // The next element is still not found, exit the loop
                    _logger?.LogError("Could not find the next element!");
                    break;
                }
            }
            elementVm = nextElementVm;

            if (!elementVm.IsExpanded) {
                elementVm.IsExpanded = true;
            }
        }
        // Select the last element
        SelectedItem = elementVm;
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
}