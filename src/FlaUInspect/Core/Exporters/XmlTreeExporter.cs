using System.Xml.Linq;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Core.Exporters;

public class XmlTreeExporter(bool enableXPath) : ITreeExporter {

    public string Export(ElementViewModel element) {
        XDocument document = new ();
        document.Add(new XElement("Root"));
        ExportElement(document.Root!, element);
        return document.ToString();
    }

    private void ExportElement(XElement parent, ElementViewModel element) {
        XElement xElement = CreateXElement(element);
        parent.Add(xElement);

        try {
            foreach (ElementViewModel children in element.LoadChildren()) {
                try {
                    ExportElement(xElement, children!);
                } catch {
                    // ignored
                }
            }
        } catch {
            // ignored
        }
    }

    private XElement CreateXElement(ElementViewModel element) {

        List<XAttribute> attrs = [
            new ("Name", element.Name),
            new ("AutomationId", element.AutomationId),
            new ("ControlType", element.ControlType)
        ];

        if (enableXPath) {
            attrs.Add(new XAttribute("XPath", element.XPath));
        }

        XElement xElement = new ("Element", attrs);
        return xElement;
    }
}