﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;

namespace FlaUInspect.Core
{
    public class HoverMode
    {
        private readonly AutomationBase _automation;
        private readonly DispatcherTimer _dispatcherTimer;
        private AutomationElement _currentHoveredElement;

        public event Action<AutomationElement> ElementHovered;

        public HoverMode(AutomationBase automation)
        {
            _automation = automation;
            _dispatcherTimer = new DispatcherTimer();
            _dispatcherTimer.Tick += DispatcherTimerTick;
            _dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
        }

        public void Start()
        {
            _currentHoveredElement = null;
            _dispatcherTimer.Start();
        }

        public void Stop()
        {
            _currentHoveredElement = null;
            _dispatcherTimer.Stop();
        }

        private void DispatcherTimerTick(object sender, EventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                var screenPos = Mouse.Position;
                try
                {
                    var hoveredElement = _automation.FromPoint(screenPos);
                    // Skip items in the current process
                    // Like Inspect itself or the overlay window
                    if (hoveredElement.Properties.ProcessId == Process.GetCurrentProcess().Id)
                    {
                        return;
                    }
                    if (!Equals(_currentHoveredElement, hoveredElement))
                    {
                        _currentHoveredElement = hoveredElement;
                        ElementHovered?.Invoke(hoveredElement);
                    }
                    else
                    {
                        ElementHighlighter.HighlightElement(hoveredElement);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    string caption = "FlaUInspect - Unauthorized access exception";
                    string message = "You are accessing a protected UI element in hover mode.\nTry to start FlaUInspect as administrator.";
                    MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch(System.IO.FileNotFoundException ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }
        }
    }
}
