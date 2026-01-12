using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Identifiers;
using FlaUInspect.Core;
using FlaUInspect.Core.Exporters;
using FlaUInspect.Core.Logger;
using FlaUInspect.Models;
using Microsoft.Win32;

namespace FlaUInspect.ViewModels;

public class ProcessViewModel : ObservableObject {

    private readonly AutomationBase _automation;
    private readonly InternalLogger _logger;
    private readonly int _processId;
    private readonly ITreeWalker _treeWalker;
    private readonly IntPtr _windowHandle;
    private ObservableCollection<ElementPatternItem>? _elementPatterns;
    private FocusTrackingMode? _focusTrackingMode;
    private PatternItemsFactory? _patternItemsFactory;
    private AutomationElement? _rootElement;
    private ElementOverlay _trackHighlighterOverlay;

    public ProcessViewModel(AutomationBase automation, int processId, IntPtr mainWindowHandle, InternalLogger logger) {
        _logger = logger;
        _automation = automation;
        _processId = processId;
        _windowHandle = mainWindowHandle;

        _trackHighlighterOverlay = CreateTrackHighlighterOverlay();

        WindowTitle = $"Process: [{processId}] '{(processId != 0
            ? _automation.FromHandle(mainWindowHandle)?.Properties.Name ?? "N/A"
            : "Desktop")}'";

        HoverManager.AddListener(_windowHandle,
                                 x => {
                                     if (EnableHoverMode) {
                                         ElementToSelectChanged(x);
                                     }
                                 });
        HoverManager.Disable(_windowHandle);

        _treeWalker = _automation.TreeWalkerFactory.GetControlViewWalker();

        Elements = [];

        RefreshCommand = new AsyncRelayCommand(async () => await Task.Run(Initialize));
        CaptureSelectedItemCommand = new RelayCommand(_ => {
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

        CurrentElementSaveStateCommand = new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }

            try {
                ITreeExporter exporter = new XmlTreeExporter(EnableXPath);
                string exportedTree = exporter.Export(SelectedItem);

                Clipboard.SetText(exportedTree.ToString());
                CopiedNotificationCurrentElementSaveStateRequested?.Invoke();
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }
        });

        ClosingCommand = new RelayCommand(_ => {
            HoverManager.RemoveListener(_windowHandle);
            _trackHighlighterOverlay?.Dispose();
            _focusTrackingMode?.Stop();
            _focusTrackingMode = null;
        });

        CopyDetailsToClipboardCommand = new RelayCommand(_ => {
            if (SelectedItem?.AutomationElement == null) {
                return;
            }

            try {
                IElementDetailsExporter detailsExporter = new XmlElementDetailsExporter();
                string details = detailsExporter.Export(ElementPatterns);

                Clipboard.SetText(details);
                CopiedNotificationRequested?.Invoke();
            } catch (Exception e) {
                _logger?.LogError(e.ToString());
            }
        });
    }

    public string? WindowTitle { get; }


    public bool EnableXPath {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public ObservableCollection<ElementViewModel> Elements { get; private set; }
    public ObservableCollection<ElementViewModel>? FlatNodes {
        get => GetProperty<ObservableCollection<ElementViewModel>>();
        private set => SetProperty(value);
    }

    public IEnumerable<ElementPatternItem> ElementPatterns {
        get => _elementPatterns ?? Enumerable.Empty<ElementPatternItem>();
        private set => SetProperty(ref _elementPatterns, value as ObservableCollection<ElementPatternItem>);
    }

    public ElementViewModel? SelectedItem {
        get => GetProperty<ElementViewModel>();
        set {
            if (SetProperty(value)) {
                if (value != null) {
                    if (EnableHighLightSelectionMode) {
                        TrackSelectedItem(value);
                    }
                    Task.Run(() => ReadPatternsForSelectedItem(value.AutomationElement));
                }
            }
        }
    }

    public bool EnableHoverMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    public bool EnableHighLightSelectionMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    public ICommand ClosingCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CaptureSelectedItemCommand { get; }
    public ICommand CurrentElementSaveStateCommand { get; }
    public ICommand CopyDetailsToClipboardCommand { get; }

    public bool EnableFocusTrackingMode {
        get => GetProperty<bool>();
        set {
            SetProperty(value);
            SetMode();
        }
    }

    private static ElementOverlay CreateTrackHighlighterOverlay() {
        return App.FlaUiAppOptions.SelectionOverlay() ?? App.FlaUiAppOptions.DefaultOverlay()!;
    }

    private void TrackSelectedItem(ElementViewModel item) {
        if (item.AutomationElement != null) {
            _trackHighlighterOverlay?.Dispose();
            _trackHighlighterOverlay = CreateTrackHighlighterOverlay();

            try {
                _trackHighlighterOverlay.Show(item.AutomationElement.Properties.BoundingRectangle.Value);
            } catch (Exception e) {
                _trackHighlighterOverlay?.Dispose();
            }
        }
    }

    private void SetMode() {
        HoverManager.Disable(_windowHandle);
        _trackHighlighterOverlay?.Dispose();
        _focusTrackingMode?.Stop();

        if (new[] { EnableHoverMode, EnableHighLightSelectionMode, EnableFocusTrackingMode }.Count(x => x) == 1) {
            if (EnableFocusTrackingMode) {
                _focusTrackingMode?.Start();
            } else if (EnableHighLightSelectionMode) {
                if (SelectedItem != null) {
                    TrackSelectedItem(SelectedItem);
                }
            } else if (EnableHoverMode) {
                HoverManager.Enable(_windowHandle);
            }
        }
    }

    public event Action? CopiedNotificationCurrentElementSaveStateRequested;
    public event Action CopiedNotificationRequested;

    public void Initialize() {
        _patternItemsFactory = new PatternItemsFactory(_automation);

        _rootElement = _windowHandle == IntPtr.Zero
            ? _automation.GetDesktop()
            : _automation.FromHandle(_windowHandle);

        ElementViewModel desktopViewModel = new (_rootElement, null, 0, _logger);

        List<ElementViewModel> topChildren = desktopViewModel.LoadChildren();

        Elements = new ObservableCollection<ElementViewModel>(topChildren);

        // Initialize hover
        EnableHoverMode = false;

        // Initialize focus tracking
        _focusTrackingMode = new FocusTrackingMode(_automation,
                                                   x => {
                                                       if (EnableFocusTrackingMode) {
                                                           ElementToSelectChanged(x);
                                                       }
                                                   });

        ElementPatterns = GetDefaultPatternList();
        SelectedItem = Elements.Count == 0 ? null : Elements[0];

        OnPropertyChanged(nameof(Elements));
        OnPropertyChanged(nameof(ElementPatterns));
    }

    public void ElementToSelectChanged(AutomationElement? obj, bool forceExpand = false) {
        Stack<AutomationElement> pathToRoot = new ();

        while (obj != null && obj.Properties.ProcessId == _processId) {
            // Break on circular relationship (should not happen?)
            if (pathToRoot.Contains(obj) || obj.Equals(_rootElement)) {
                break;
            }

            pathToRoot.Push(obj);

            if (forceExpand) {
                break;
            }

            try {
                obj = _treeWalker.GetParent(obj);
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }
        }

        IEnumerable<ElementViewModel> viewModels = Elements;
        ElementViewModel? nextElementVm = null;

        while (pathToRoot.Count > 0) {
            AutomationElement elementOnPath = pathToRoot.Pop();
            nextElementVm = FindElement(viewModels, elementOnPath);

            if (nextElementVm != null && (forceExpand || !nextElementVm.IsExpanded)) {
                if (pathToRoot.Count != 0) {
                    nextElementVm.IsExpanded = true;
                }
                ExpandElement(nextElementVm);

                if (forceExpand) {
                    break;
                }
            }
        }

        SelectedItem = nextElementVm;
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


    private void ReadPatternsForSelectedItem(AutomationElement? selectedItemAutomationElement) {
        if (SelectedItem?.AutomationElement == null || selectedItemAutomationElement == null) {
            return;
        }

        if (_patternItemsFactory == null) {
            return;
        }

        try {
            HashSet<PatternId> supportedPatterns = [.. selectedItemAutomationElement.GetSupportedPatterns()];
            IDictionary<string, PatternItem[]> patternItemsForElement = _patternItemsFactory.CreatePatternItemsForElement(selectedItemAutomationElement, supportedPatterns);

            foreach (ElementPatternItem elementPattern in ElementPatterns) {
                elementPattern.IsVisible = elementPattern.PatternIdName == PatternItemsFactory.Identification
                                           || elementPattern.PatternIdName == PatternItemsFactory.Details
                                           || elementPattern.PatternIdName == PatternItemsFactory.PatternSupport
                                           || supportedPatterns.Any(x => x.Name.Equals(elementPattern.PatternIdName));


                elementPattern.Children = patternItemsForElement.TryGetValue(elementPattern.PatternIdName, out PatternItem[]? children)
                    ? new ObservableCollection<PatternItem>(children)
                    : [];

                if (!elementPattern.Children.Any()) {
                    elementPattern.IsVisible = false;
                }
            }
        } catch (Exception e) {
            _logger?.LogError(e.ToString());
        }
    }

    public void ExpandElement(ElementViewModel sender) {
        List<ElementViewModel> children = sender.LoadChildren();
        children.Reverse();

        int senderIndex = Elements.IndexOf(sender);

        if (senderIndex < 0) {
            return;
        }

        foreach (ElementViewModel child in children) {
            Elements.Insert(senderIndex + 1, child);
        }
    }

    public void CollapseElement(ElementViewModel sender) {
        int senderIndex = Elements.IndexOf(sender);

        if (senderIndex < 0) {
            return;
        }

        var removeCount = 0;

        for (int i = senderIndex + 1; i < Elements.Count; i++) {
            if (IsDescendantOf(Elements[i], sender)) {
                removeCount++;
            } else {
                break;
            }
        }

        for (var i = 0; i < removeCount; i++) {
            Elements.RemoveAt(senderIndex + 1);
        }
    }

    private bool IsDescendantOf(ElementViewModel? node, ElementViewModel? parent) {
        if (node == null || parent == null) {
            return false;
        }
        ElementViewModel? p = node.Parent;

        while (p != null) {
            if (p == parent)
                return true;
            p = p.Parent;
        }
        return false;
    }
}