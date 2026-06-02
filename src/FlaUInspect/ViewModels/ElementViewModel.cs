using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;
using FlaUInspect.Core.Logger;

namespace FlaUInspect.ViewModels;

public class ElementViewModel : ObservableObject {
	private readonly ILogger? _logger;

	public ElementViewModel(AutomationElement? automationElement, ElementViewModel? parent, int level, ILogger? logger, int loadSubChildren) {
		Level = level;
		_logger = logger;
		AutomationElement = automationElement;
		Parent = parent;

		Name = (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
		AutomationId = (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();
		ControlType = AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out var value) ? value : ControlType.Unknown;
		Children = loadSubChildren > 0 ? LoadChildren(--loadSubChildren) : [];
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

	public List<ElementViewModel> Children { get; private set; }

	public override string ToString() => $"{Name} [{ControlType}] : {AutomationId}";

	public List<ElementViewModel> LoadChildren(int loadSubChildren = 0) {
		if (AutomationElement == null)
			return [];

		try {
			using (CacheRequest.ForceNoCache()) {
				return [.. AutomationElement.FindAllChildren().Select(element => new ElementViewModel(element, this, Level + 1, _logger, loadSubChildren))];
			}
		}
		catch (Exception ex) {
			_logger?.LogError($"Exception: {ex.Message}");
			return [];
		}
	}

	public override bool Equals(object? obj) => Equals(obj as ElementViewModel);

	public bool Equals(ElementViewModel? y) => y is not null
		&& (ReferenceEquals(this, y)
			|| (GetType() == y.GetType()
				&& Level == y.Level
				&& AutomationElement == y.AutomationElement
				&& Parent! == y.Parent!));

	public override int GetHashCode() => (Level, AutomationId, ControlType, Name).GetHashCode();

	public static bool operator ==(ElementViewModel lhs, ElementViewModel rhs) => lhs is null
			? rhs is null
			// Equals handles case of null on right side.
			: lhs.Equals(rhs);

	public static bool operator !=(ElementViewModel lhs, ElementViewModel rhs) => !(lhs == rhs);
}