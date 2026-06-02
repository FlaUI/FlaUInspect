using System.Collections.ObjectModel;
using FlaUInspect.Core;
using FlaUInspect.ViewModels;

namespace FlaUInspect.Models;

public class ElementPatternItem(string patternName, string patternIdName, bool isVisible = true, bool isExpanded = false) : ObservableObject {
	public string PatternName { get; private set; } = patternName;
	public string PatternIdName { get; } = patternIdName;

	public bool IsVisible {
		get;
		set => SetProperty(ref field, value);
	} = isVisible;
	public bool IsExpanded {
		get;
		set => SetProperty(ref field, value);
	} = isExpanded;

	// ReSharper disable once CollectionNeverQueried.Global
	public ObservableCollection<PatternItem>? Children {
		get => GetProperty<ObservableCollection<PatternItem>>();
		set => SetProperty(value);
	}
}