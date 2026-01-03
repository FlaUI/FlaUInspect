using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NotSupportedException = FlaUI.Core.Exceptions.NotSupportedException;

namespace FlaUInspect.Core.Converters;

public class IntToVisibilityConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is int intValue) {
            if (parameter is "EqualsZero") {
                return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
            } else if (parameter is "GreaterThanZero") {
                return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}