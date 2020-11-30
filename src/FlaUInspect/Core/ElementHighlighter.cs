using System;
using System.Drawing;
using System.Threading.Tasks;
using FlaUI.Core.AutomationElements;

namespace FlaUInspect.Core
{
    public static class ElementHighlighter
    {
        public static void HighlightElement(AutomationElement automationElement)
        {
            Task.Run(() => {
                try
                {
                    automationElement.DrawHighlight(false, Color.Red, TimeSpan.FromSeconds(1));
                }
                catch(FlaUI.Core.Exceptions.PropertyNotSupportedException ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            });
        }
    }
}
