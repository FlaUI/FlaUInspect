using FlaUI.Core;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;

namespace FlaUInspect.ViewModels;

public class PatternItem(string key, string? value, Action? action = null) : ObservableObject {
	public string Key {
		get;
		set => SetProperty(ref field, value);
	} = key;
	public string? Value {
		get;
		set => SetProperty(ref field, value);
	} = value;
	public bool HasExecutableAction => Action != null;
	public Action? Action { get; } = action;

	public static PatternItem FromAutomationProperty<T>(string key, IAutomationProperty<T> value) => new(key, value!.ToDisplayText());
}