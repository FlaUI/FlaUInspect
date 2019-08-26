using System;
using System.Drawing;
using System.Threading.Tasks;
using FlaUI.Core.Exceptions;
using FlaUI.Core.AutomationElements;

namespace FlaUInspect.Core
{
    public static class ElementHighlighter
    {
        public static void HighlightElement(AutomationElement automationElement)
        {
            try
            {
                Task.Run(() => automationElement.DrawHighlight(false, Color.Red));
            }
            catch (PropertyNotSupportedException ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

        }
    }
}
