using System.Globalization;
using System.Windows.Data;
using FlaUI.Core.Exceptions;

namespace FlaUInspect.Core.Converters;

public class InvertBooleanConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is bool boolValue) {
            return !boolValue;
        }
        throw new ArgumentException(@"Value must be a boolean", nameof(value));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotSupportedByFrameworkException();
    }
}