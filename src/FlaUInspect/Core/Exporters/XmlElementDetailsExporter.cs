using System.Xml.Linq;
using FlaUInspect.Models;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Core.Exporters;

public class XmlElementDetailsExporter : IElementDetailsExporter {
    public string Export(IEnumerable<ElementPatternItem> patternItems) {
        XDocument document = new ();
        XElement root = new ("Root");
        document.Add(root);

        foreach (ElementPatternItem elementPatternItem in patternItems.Where(x => x.IsVisible)) {
            XElement xElement = new ("pattern", new XAttribute("Name", elementPatternItem.PatternName));

            foreach (PatternItem patternItem in elementPatternItem.Children ?? []) {
                xElement.Add(new XElement("property", new XAttribute("Name", patternItem.Key), new XAttribute("Value", patternItem.Value)));
            }
            root.Add(xElement);
        }

        return document.ToString();
    }
}