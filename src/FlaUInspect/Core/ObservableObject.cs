using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace FlaUInspect.Core;

public abstract class ObservableObject : INotifyPropertyChanged {

    private readonly Dictionary<string, object?> _backingFieldValues = [];
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets a property value from the internal backing field
    /// </summary>
    protected T? GetProperty<T>([CallerMemberName] string? propertyName = null) {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (_backingFieldValues.TryGetValue(propertyName, out object? value)) {
            return (T)value!;
        }
        return default;
    }

    /// <summary>
    /// Saves a property value to the internal backing field
    /// </summary>
    protected bool SetProperty<T>(T? newValue, [CallerMemberName] string? propertyName = null) {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (IsEqualInternal(GetProperty<T>(propertyName), newValue)) {
            return false;
        }
        _backingFieldValues[propertyName] = newValue!;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Sets a property value to the backing field
    /// </summary>
    protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null) {
        if (IsEqualInternal(field, newValue)) {
            return false;
        }
        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged<T>(Expression<Func<T>> selectorExpression) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs((selectorExpression.Body as MemberExpression)?.Member.Name));
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool IsEqualInternal<T>(T field, T newValue) {
        // Alternative: EqualityComparer<T>.Default.Equals(field, newValue);
        return Equals(field, newValue);
    }

    public void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
        RaisePropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    protected virtual void RaisePropertyChanged(PropertyChangedEventArgs args) {
        PropertyChangedEventHandler? copy = PropertyChanged;
        if (copy != null)
            copy(this, args);
    }
}