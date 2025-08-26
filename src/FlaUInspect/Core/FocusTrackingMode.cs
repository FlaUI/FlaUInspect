using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.EventHandlers;
using Application = System.Windows.Application;

namespace FlaUInspect.Core;

public class FocusTrackingMode(AutomationBase? automation) {
    private AutomationElement? _currentFocusedElement;
    private FocusChangedEventHandlerBase? _eventHandler;

    public event Action<AutomationElement?>? ElementFocused;

    public void Start() {
        // Might give problems because inspect is registered as well.
        // MS recommends to call UIA commands on a thread outside a UI thread.
        Task.Factory.StartNew(() => _eventHandler = automation?.RegisterFocusChangedEvent(OnFocusChanged));
    }

    public void Stop() {
        if (_eventHandler != null) {
            automation?.UnregisterFocusChangedEvent(_eventHandler);
        }
    }

    private void OnFocusChanged(AutomationElement? automationElement) {
        // Skip items in the current process
        // Like Inspect itself or the overlay window
        if (automationElement?.Properties.ProcessId == Environment.ProcessId) {
            return;
        }

        if (!Equals(_currentFocusedElement, automationElement)) {
            _currentFocusedElement = automationElement;
            Application.Current.Dispatcher.Invoke(() => {
                ElementFocused?.Invoke(automationElement);
            });
        }
    }
}