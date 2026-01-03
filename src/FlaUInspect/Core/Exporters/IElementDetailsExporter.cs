using FlaUInspect.Models;

namespace FlaUInspect.Core.Exporters;

public interface IElementDetailsExporter {

    string Export(IEnumerable<ElementPatternItem> automationElement);
}