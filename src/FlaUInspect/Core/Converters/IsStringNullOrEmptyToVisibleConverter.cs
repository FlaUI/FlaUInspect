using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class IsStringNullOrEmptyToVisibleConverter : IValueConverter {
    public bool IsInverted { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        var str = value as string;
        bool condition = string.IsNullOrEmpty(str);
        condition = IsInverted ? !condition : condition;

        return condition ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}