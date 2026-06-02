using FlaUInspect.ViewModels;

namespace FlaUInspect.Core.Exporters;

public interface IElementDetailsExporter {
	string Export(ProcessViewModel processViewModel, int depthMax = 1);
}