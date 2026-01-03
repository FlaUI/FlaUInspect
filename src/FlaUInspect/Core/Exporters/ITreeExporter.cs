using FlaUInspect.ViewModels;

namespace FlaUInspect.Core.Exporters;

public interface ITreeExporter {
    string Export(ElementViewModel element);
}