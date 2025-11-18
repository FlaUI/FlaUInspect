using System.Windows.Input;
using System.Windows.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUInspect.Core.Logger;
using Mouse = FlaUI.Core.Input.Mouse;
using Point = System.Drawing.Point;

namespace FlaUInspect.Core;

public class HoverMode : IDisposable {
    private readonly AutomationBase? _automation;
    private readonly DispatcherTimer _dispatcherTimer;
    private readonly ILogger? _logger;
    private AutomationElement? _currentHoveredElement;

    public HoverMode(AutomationBase? automation, ILogger? logger) {
        _automation = automation;
        _logger = logger;
        _dispatcherTimer = new DispatcherTimer();
        _dispatcherTimer.Tick += DispatcherTimerTick;
        _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
    }

    public event Action<AutomationElement?>? ElementHovered;

    public void Start() {
        _currentHoveredElement = null;
        _dispatcherTimer.Start();
    }

    public void Stop() {
        _currentHoveredElement = null;
        _dispatcherTimer.Stop();
    }

    private void DispatcherTimerTick(object? sender, EventArgs e) {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
            Point screenPos = Mouse.Position;

            try {
                AutomationElement? hoveredElement = _automation?.FromPoint(screenPos);

                // Skip items in the current process
                // Like Inspect itself or the overlay window
                if (hoveredElement?.Properties.ProcessId == Environment.ProcessId) {
                    return;
                }

                if (!Equals(_currentHoveredElement, hoveredElement)) {
                    _currentHoveredElement = hoveredElement;
                    ElementHovered?.Invoke(hoveredElement);
                } else {
                    ElementHighlighter.HighlightElement(hoveredElement, _logger);
                }
            } catch (UnauthorizedAccessException) {
                _logger?.LogError("You are accessing a protected UI element in hover mode.\nTry to start FlaUInspect as administrator.");
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }
        }
    }

    public void Dispose() {
        _automation?.Dispose();
        _dispatcherTimer.Tick -= DispatcherTimerTick;
    }
}