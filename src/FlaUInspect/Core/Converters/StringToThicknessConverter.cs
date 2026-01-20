using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class StringToThicknessConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is string str && !string.IsNullOrWhiteSpace(str)) {
            string[] parts = str.Split(new char[] { ',', ' ' });

            if (parts.Length == 4 &&
                double.TryParse(parts[0], out double left) &&
                double.TryParse(parts[1], out double top) &&
                double.TryParse(parts[2], out double right) &&
                double.TryParse(parts[3], out double bottom)) {
                return new Thickness(left, top, right, bottom);
            }

            if (parts.Length == 2 &&
                double.TryParse(parts[0], out double leftRigth) &&
                double.TryParse(parts[1], out double topBottom)) {
                return new Thickness(leftRigth, topBottom, leftRigth, topBottom);
            }
        }
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is Thickness thickness) {
            return $"{thickness.Left},{thickness.Top},{thickness.Right},{thickness.Bottom}";
        }
        return string.Empty;
    }
}