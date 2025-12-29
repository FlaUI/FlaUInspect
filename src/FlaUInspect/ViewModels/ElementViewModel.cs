using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;
using FlaUInspect.Core.Logger;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MenuItem = System.Windows.Controls.MenuItem;
namespace FlaUInspect.ViewModels;

public class ElementViewModel(AutomationElement? automationElement, ILogger? logger) : ObservableObject {
    private readonly object _lockObject = new ();
    public AutomationElement? AutomationElement { get; } = automationElement;
    private RelayCommand? _refreshItemCommand;
    private RelayCommand? _focusCommand;

    public bool IsExpanded {
        get => GetProperty<bool>();
        set {
            SetProperty(value);

            if (value && (Children.Count == 0 || Children[0] == null)) {
                LoadChildren(0);
            }
        }
    }

    public bool IsSelected {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public string Name => (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();

    public string AutomationId => (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();

    public ControlType ControlType => AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out ControlType value) ? value : ControlType.Custom;

    public ExtendedObservableCollection<ElementViewModel?> Children { get; set; } = [];


    public ICommand RefreshItemCommand =>
        _refreshItemCommand ??= new((_) => {
            Children.Clear();
            IsExpanded = true;
        });

    public ICommand FocusCommand =>
        _focusCommand ??= new((_) => {
            try {
                AutomationElement.Focus();
            } catch { }
        });


    private ObservableCollection<MenuItem>? _mouseActions;
    public ObservableCollection<MenuItem> MouseActions { get => _mouseActions ??= BuildMouseActions(); }

    private ObservableCollection<MenuItem> BuildMouseActions() {
        return [
            CreateMenuItem("Left Click", () => AutomationElement?.Click()),
            CreateMenuItem("Right Click", () => AutomationElement?.RightClick()),
            CreateMenuItem("Double Click", () => AutomationElement?.DoubleClick()),
        ];
    }

    private MenuItem CreateMenuItem(string header, Action value) {
        return new MenuItem {
            Header = header,
            Command = new RelayCommand(_ => value())
        };
    }

    public string XPath => AutomationElement == null ? string.Empty : Debug.GetXPathToElement(AutomationElement);
    public event Action<ElementViewModel>? SelectionChanged;

    public void LoadChildren(int level) {
        lock (_lockObject) {
            if (Children is { Count: > 0 } && Children[0] == null) {
                Children.Clear();
            }

            foreach (ElementViewModel? child in Children) {
                child!.SelectionChanged -= SelectionChanged;
            }

            List<ElementViewModel?> childrenViewModels = [];

            try {
                if (AutomationElement != null) {
                    foreach (AutomationElement child in AutomationElement.FindAllChildren()) {
                        ElementViewModel childViewModel = new (child, logger);
                        childViewModel.Children.Add(null);

                        childViewModel.SelectionChanged += SelectionChanged;
                        childrenViewModels.Add(childViewModel);

                        if (level > 0) {
                            childViewModel.LoadChildren(level - 1);

                        }
                    }
                }
            } catch (Exception ex) {
                logger?.LogError($"Exception: {ex.Message}");
            }

            Children.Reset(childrenViewModels);
        }
    }
}