using System.Windows.Input;
using System.Windows.Threading;
using FlaUI.Core;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;
using Mouse = FlaUI.Core.Input.Mouse;

namespace FlaUInspect.Core;

public static class HoverManager {
	private static Func<ElementOverlay?>? _elementOverlayFunc;
	private static AutomationBase? _automationBase;
	private static AutomationElement? _hoveredElement;
	private static ElementOverlay? _elementOverlay;

	private static readonly List<KeyValuePair<IntPtr, Action<AutomationElement?>>> _listeners = [];

	private static readonly HashSet<IntPtr> _enabledListeners = [];

	private static readonly Lock _lockObject = new();

	static HoverManager() {
		DispatcherTimer timer = new() {
			Interval = TimeSpan.FromMilliseconds(300)
		};
		timer.Tick += (s, e) => Refresh();
		timer.Start();
	}

	public static bool IsInitialized => _automationBase != null && _elementOverlayFunc != null;

	private static void Refresh() {
		if (_enabledListeners.Count == 0) {
			_elementOverlay?.Dispose();
			_hoveredElement = null;
			return;
		}

		if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
			return;

		var screenPos = Mouse.Position;
		try {
			var automationElement = _automationBase?.FromPoint(screenPos);
			if (automationElement == null || (_hoveredElement != null && automationElement.Equals(_hoveredElement)))
				return;

			_elementOverlay?.Dispose();

			if (automationElement.Properties.ProcessId == Environment.ProcessId) {
				_hoveredElement = null;
				return;
			}
			_hoveredElement = automationElement;

			foreach (var keyValuePair in _listeners)
				try {
					keyValuePair.Value?.Invoke(automationElement);
				}
				catch {
					// ignored
				}

			try {
				if (_elementOverlayFunc != null && _enabledListeners.Count > 0) {
					var elementOverlay = _elementOverlayFunc();
					elementOverlay?.Show(automationElement.Properties.BoundingRectangle.Value);
					_elementOverlay = elementOverlay;
				}
			}
			catch {
				// ignored
			}
		}
		catch {
			// ignored
		}
	}

	public static void AddListener(IntPtr id, Action<AutomationElement?> onElementHovered) {
		lock (_lockObject)
			_listeners.Add(new KeyValuePair<IntPtr, Action<AutomationElement?>>(id, onElementHovered));
	}

	public static void RemoveListener(IntPtr id) {
		lock (_lockObject)
			if (_listeners.FirstOrDefault(x => x.Key == id) is KeyValuePair<IntPtr, Action<AutomationElement?>> pair)
				_ = _listeners.Remove(pair);
	}

	public static void Enable(IntPtr item) {
		lock (_lockObject)
			_ = _enabledListeners.Add(item);
	}

	public static void Disable(IntPtr item) {
		lock (_lockObject)
			_ = _enabledListeners.Remove(item);
	}

	public static void Initialize(AutomationBase? automation, Func<ElementOverlay?> elementOverlayFunc) {
		_automationBase = automation;
		_elementOverlayFunc = elementOverlayFunc;
	}
}