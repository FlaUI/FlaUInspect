using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaUI.UIA3.Converters;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;
using FlaUInspect.Core.Logger;
using Interop.UIAutomationClient;
using TreeScope = Interop.UIAutomationClient.TreeScope;

namespace FlaUInspect.ViewModels;

public class ElementViewModel : ObservableObject {
    private readonly ILogger? _logger;

    public ElementViewModel(AutomationElement? automationElement, ElementViewModel? parent, ILogger? logger) {
        _logger = logger;
        AutomationElement = automationElement;
        Parent = parent;
        
        _guidId = Guid.NewGuid().ToString() + (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();;
        
    }
    private readonly object _lockObject = new ();
    private readonly string _guidId;
    public AutomationElement? AutomationElement { get; }
    public ElementViewModel? Parent { get; }

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
    // public string Name {
    //     get {
    //         try {
    //             return (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
    //         } catch (Exception e) {
    //             Console.WriteLine(e);
    //             return string.Empty;
    //         }
    //     }
    // }

    public string AutomationId => (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();
    // public string AutomationId {
    //     get {
    //         try {
    //             return (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();
    //         } catch (Exception e) {
    //             Console.WriteLine(e);
    //             return string.Empty;
    //         }
    //     }
    // }

    public ControlType ControlType => AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out ControlType value) ? value : ControlType.Unknown;
    // public ControlType ControlType {
    //     get {
    //         try {
    //             return AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out ControlType value)
    //                 ? value
    //                 : ControlType.Custom;
    //         } catch (Exception e) {
    //             Console.WriteLine(e);
    //             return ControlType.Unknown;
    //         }
    //     }
    // }

    public ExtendedObservableCollection<ElementViewModel?> Children { get; set; } = [];


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
                    // IUIAutomationElementArray? uiAutomationElementArray = (AutomationElement.FrameworkAutomationElement as UIA3FrameworkAutomationElement)
                    //                                                       ?.NativeElement.FindAll(TreeScope.TreeScope_Children,
                    //                                                                               new UIA3Automation().NativeAutomation.CreateTrueCondition());
                    // AutomationElement[] nativeArrayToManaged = AutomationElementConverter.NativeArrayToManaged(new UIA3Automation(), uiAutomationElementArray);

                    using (CacheRequest.ForceNoCache()) {
                        AutomationElement[] children = AutomationElement.FindAllChildren();


                        foreach (AutomationElement child in children) {
                            ElementViewModel childViewModel = new (child, this, _logger);
                            childViewModel.Children.Add(null);

                            childViewModel.SelectionChanged += SelectionChanged;
                            childrenViewModels.Add(childViewModel);

                            if (level > 0) {
                                childViewModel.LoadChildren(level - 1);

                            }
                        }
                    }
                }
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }

            Children.Reset(childrenViewModels);
        }
    }

    public void ExpandAll() {
        foreach (ElementViewModel? child in Children) {
            if (child is not null) {
                child.IsExpanded = true;
                child?.ExpandAll();
            }
        }
    }

    public void ClearEvents() {
        this.SelectionChanged -= SelectionChanged;
    }

    public void ExpandToXPath(List<string> xpath) {
        LoadChildren(0);
        SetProperty(true, nameof(IsExpanded));

        if (xpath.Count == 0) {
            return;
        }

        var match = Regex.Match(xpath[0], @"(\w+)(\[(\d+)\])?");

        if (!match.Success) {
            return;
        }
        var type = match.Groups[1].Value;
        var idx = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 1;

        var controlType = Enum.TryParse<ControlType>(type, out var ct) ? ct : ControlType.Custom;
        ElementViewModel? viewModel = Children.Where(x => x.ControlType == controlType).Skip(idx - 1).FirstOrDefault();

        viewModel?.ExpandToXPath(xpath.Skip(1).ToList());
    }
}