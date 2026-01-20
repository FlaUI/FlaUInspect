using System.Reflection;

namespace FlaUInspect.Core;

public class Editable<T> : ObservableObject where T : class, new() {
    private readonly Action<T, T> _apply;

    private readonly Func<T, T> _clone;
    private readonly Func<T, T, bool> _equals;

    public Editable(T original, Func<T, T> clone, Action<T, T> apply, Func<T, T, bool> equals) {
        Original = original;
        _clone = clone;
        _apply = apply;
        _equals = equals;

        Current = clone(original);
    }

    public T Original { get; }
    public T Current { get; }

    public bool IsDirty => !_equals(Current, Original);

    public void Apply(object? obj) {
        _apply(Current, Original);
        RaiseDirty();
    }

    public void Reset(object? obj) {
        Copy(_clone(Original), Current);
        RaiseDirty();
    }

    private void RaiseDirty() {
        OnPropertyChanged(nameof(IsDirty));
    }

    private static void Copy(T from, T to) {
        foreach (PropertyInfo p in typeof(T).GetProperties()
                                            .Where(p => p.CanRead && p.CanWrite)) {
            p.SetValue(to, p.GetValue(from));
        }
    }
}