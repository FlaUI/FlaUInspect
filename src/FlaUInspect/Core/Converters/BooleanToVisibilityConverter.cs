using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class BooleanToVisibilityConverter : IValueConverter {

    public Visibility TrueVisibility { get; set; } = Visibility.Visible;
    public Visibility FalseVisibility { get; set; } = Visibility.Collapsed;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is bool boolValue) {
            return boolValue ? TrueVisibility : FalseVisibility;
        }
        throw new ArgumentException("Value must be a boolean or nullable boolean", nameof(value));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}