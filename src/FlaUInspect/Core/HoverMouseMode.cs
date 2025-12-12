// // Description :    Definition of HoverMouseMode.cs class
// //
// // Copyright © 2025 - 2025, Alcon. All rights reserved.

using System.Drawing;
using System.Windows.Input;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Mouse = FlaUI.Core.Input.Mouse;
using Point = System.Drawing.Point;

namespace FlaUInspect.Core;

public class HoverMouseMode {
    private readonly AutomationBase _automationBase;
    private ElementOverlay? _elementOverlay;
    private AutomationElement? _hoveredElement;

    // Add a field to track the last refresh time
    private DateTime _lastRefresh = DateTime.MinValue;

    public HoverMouseMode(AutomationBase automationBase) {
        _automationBase = automationBase;

    }

    public bool IsEnabled { get; set; }

    public void Refresh() {
        // Throttle: only allow refresh every 300ms
        if ((DateTime.Now - _lastRefresh).TotalMilliseconds < 300) {
            return;
        }
        _lastRefresh = DateTime.Now;

        if (!IsEnabled) {
            _elementOverlay?.Dispose();
            _hoveredElement = null;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
            Point screenPos = Mouse.Position;

            try {
                AutomationElement? automationElement = _automationBase?.FromPoint(screenPos);

                if (automationElement == null) {
                    _elementOverlay?.Dispose();
                    _hoveredElement = null;
                    return;
                }

                if (automationElement?.Properties.ProcessId == Environment.ProcessId) {
                    _elementOverlay?.Dispose();
                    _hoveredElement = null;
                    return;
                }

                if (_hoveredElement == null || !automationElement.Equals(_hoveredElement)) {
                    _elementOverlay?.Dispose();
                    _hoveredElement = automationElement;
                
                    try {
                        ElementOverlay elementOverlay = new ( new ElementOverlayConfiguration(0, 0, Color.FromArgb(25, Color.Red), ElementOverlay.FillRectangleFactory));
                         elementOverlay.Show(automationElement.Properties.BoundingRectangle.Value);
                         _elementOverlay = elementOverlay;
                    } catch {
                        // ignored
                    }
                }
            } catch {
                // ignored
            }
        }
    }
}