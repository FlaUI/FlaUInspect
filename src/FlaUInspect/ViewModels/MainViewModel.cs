using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Input;
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
    private readonly InternalLogger? _logger;
    private AutomationBase? _automation;
    private RelayCommand? _captureSelectedItemCommand;
    private RelayCommand? _closeInfoCommand;
    private ObservableCollection<ElementPatternItem>? _elementPatterns = [];
    private FocusTrackingMode? _focusTrackingMode;
    private HoverMode? _hoverMode;
    private RelayCommand? _infoCommand;
    private RelayCommand? _openErrorListCommand;
    private PatternItemsFactory? _patternItemsFactory;
    private RelayCommand? _refreshCommand;
    private AutomationElement? _rootElement;
    private RelayCommand? _startNewInstanceCommand;
    private ITreeWalker? _treeWalker;

    public SearchViewModel Search { get; }

    public MainViewModel(AutomationType automationType, string applicationVersion, InternalLogger logger) {
        _logger = logger;
        ApplicationVersion = applicationVersion;
        _logger.LogEvent += (_, _) => {
            Application.Current.Dispatcher.Invoke(() => ErrorCount = _logger.Messages.Count);
        };

        SelectedAutomationType = automationType;
        Elements = [];
        BindingOperations.EnableCollectionSynchronization(Elements, _itemsLock);

        Search = new SearchViewModel(
            () => SelectedItem,
            () => Elements.FirstOrDefault(),
            () => _treeWalker,
            ElementToSelectChanged);
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
                if (!Search.IsNavigating) {
                    Search.NotifySelectionChanged();
                }
            }
        }
    }


    public IEnumerable<ElementPatternItem> ElementPatterns {
        get => _elementPatterns ?? Enumerable.Empty<ElementPatternItem>();
        private set => SetProperty(ref _elementPatterns, value as ObservableCollection<ElementPatternItem>);
    }

    public ObservableCollection<System.Windows.Controls.MenuItem> PatternActionContextItems {get; } = new();

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

            PatternActionContextItems.Clear();
            foreach (ElementPatternItem elementPattern in ElementPatterns) {
                elementPattern.IsVisible = elementPattern.PatternIdName == PatternItemsFactory.Identification
                                           || elementPattern.PatternIdName == PatternItemsFactory.Details
                                           || elementPattern.PatternIdName == PatternItemsFactory.PatternSupport
                                           || supportedPatterns.Any(x => x.Name.Equals(elementPattern.PatternIdName));
                elementPattern.Children.Clear();

                if (patternItemsForElement.TryGetValue(elementPattern.PatternIdName, out PatternItem[]? children)) {
                    foreach (PatternItem patternItem in children) {
                        elementPattern.Children.Add(patternItem);
                        if (patternItem.HasExecutableAction) {
                             System.Windows.Controls.MenuItem actionMenuItem = new() {
                                Header = $"{patternItem.Key}"
                            };
                            actionMenuItem.Click += (_, _) => patternItem.Action?.Invoke();
                            PatternActionContextItems.Add(actionMenuItem);
                        }
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
        ElementViewModel desktopViewModel = new (_rootElement, _logger);

        desktopViewModel.SelectionChanged += obj => {
            SelectedItem = obj;
        };
        desktopViewModel.LoadChildren(0);

        lock (_itemsLock) {
            Elements.Add(desktopViewModel);
            desktopViewModel.IsExpanded = true;
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
        SelectedItem = Elements[0];

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