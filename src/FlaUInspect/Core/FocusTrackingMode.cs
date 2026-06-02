using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.EventHandlers;
using Application = System.Windows.Application;

namespace FlaUInspect.Core;

public class FocusTrackingMode(AutomationBase? automation, Func<AutomationElement, AutomationElement?> onFocusChangedAction) {
	private AutomationElement? _currentFocusedElement;
	private FocusChangedEventHandlerBase? _eventHandler;

	public void Start()
		// Might give problems because inspect is registered as well.
		// MS recommends to call UIA commands on a thread outside a UI thread.
		=> _eventHandler = automation?.RegisterFocusChangedEvent(OnFocusChanged);

	public void Stop() {
		if (_eventHandler != null)
			automation?.UnregisterFocusChangedEvent(_eventHandler);
		automation?.UnregisterAllEvents();
	}

	private void OnFocusChanged(AutomationElement? automationElement) {
		// Skip items in the current process
		// Like Inspect itself or the overlay window
		try {
			if (automationElement?.Properties.ProcessId.IsSupported != true || automationElement.Properties.ProcessId == Environment.ProcessId)
				return;
		}
		catch (Exception) {
			// Silent fail
			return;
		}

		if (!Equals(_currentFocusedElement, automationElement)) {
			_currentFocusedElement = Application.Current.Dispatcher.Invoke(() => onFocusChangedAction(automationElement));
		}
	}
}