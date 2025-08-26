using FlaUI.Core;
using FlaUInspect.Core;
using FlaUInspect.Core.Extensions;

namespace FlaUInspect.ViewModels;

public class PatternItem(string key, string? value, Action? action = null) : ObservableObject {
    private string _key = key;
    private string? _value = value;

    public string Key {
        get => _key;
        set => SetProperty(ref _key, value);
    }
    public string? Value {
        get => _value;
        set => SetProperty(ref _value, value);
    }
    public bool HasExecutableAction {
        get => Action != null;
    }
    public Action? Action { get; } = action;

    public static PatternItem FromAutomationProperty<T>(string key, IAutomationProperty<T> value) {
        return new PatternItem(key, value!.ToDisplayText());
    }
}