using System.Collections.ObjectModel;
using FlaUInspect.Core;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Models;

public class ElementPatternItem(string patternName, string patternIdName, bool isVisible = true, bool isExpanded = false)
    : ObservableObject {
    private bool _isExpanded = isExpanded;
    private bool _isVisible = isVisible;

    public string PatternName { get; private set; } = patternName;
    public string PatternIdName { get; } = patternIdName;

    public bool IsVisible {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
    public bool IsExpanded {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    // ReSharper disable once CollectionNeverQueried.Global
    public ObservableCollection<PatternItem> Children { get; } = [];
}