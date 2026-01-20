using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;
using FlaUInspect.Core.Logger;

namespace FlaUInspect.ViewModels;

public class ElementViewModel : ObservableObject {
    private readonly string _guidId;

    private readonly object _lockObject = new ();
    private readonly ILogger? _logger;

    public ElementViewModel(AutomationElement? automationElement, ElementViewModel? parent, int level, ILogger? logger) {
        Level = level;
        _logger = logger;
        AutomationElement = automationElement;
        Parent = parent;

        _guidId = Guid.NewGuid().ToString() + (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
        Name = (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
        AutomationId = (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();
        ControlType = AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out ControlType value) ? value : ControlType.Unknown;

    }

    public AutomationElement? AutomationElement { get; }
    public ElementViewModel? Parent { get; }

    public bool IsExpanded {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public bool IsSelected {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public int Level { get; }

    public string Name { get; }

    public string AutomationId { get; }

    public ControlType ControlType { get; }
    public string XPath => AutomationElement == null ? string.Empty : Debug.GetXPathToElement(AutomationElement);

    public override string ToString() {
        return $"{Name} [{ControlType}] : {AutomationId}";
    }

    public List<ElementViewModel> LoadChildren() {
        {


            try {
                if (AutomationElement != null) {
                    using (CacheRequest.ForceNoCache()) {
                        AutomationElement[] elements = AutomationElement.FindAllChildren();

                        return elements.Select(element => new ElementViewModel(element, this, Level + 1, _logger)).ToList();
                    }
                }
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }

            return [];
        }
    }
}