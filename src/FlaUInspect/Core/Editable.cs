namespace FlaUInspect.Core;

public class Editable<T>(T original, Func<T, T> clone, Action<T, T> apply, Func<T, T, bool> equals) : ObservableObject where T : class, new() {
	private readonly Action<T, T> _apply = apply;
	private readonly Func<T, T> _clone = clone;
	private readonly Func<T, T, bool> _equals = equals;

	public T Original { get; } = original;
	public T Current { get; } = clone(original);

	public bool IsDirty => !_equals(Current, Original);

	public void Apply(object? obj) {
		_apply(Current, Original);
		RaiseDirty();
	}

	public void Reset(object? obj) {
		Copy(_clone(Original), Current);
		RaiseDirty();
	}

	private void RaiseDirty() => OnPropertyChanged(nameof(IsDirty));

	private static void Copy(T from, T to) {
		foreach (var p in typeof(T).GetProperties()
											.Where(p => p.CanRead && p.CanWrite))
			p.SetValue(to, p.GetValue(from));
	}
}