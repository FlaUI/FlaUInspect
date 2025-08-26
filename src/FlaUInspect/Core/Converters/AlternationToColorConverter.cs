using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FlaUInspect.Core.Converters;

public class AlternationToColorConverter : IMultiValueConverter {
    public Brush Brush { get; set; } = new SolidColorBrush(Colors.LightGray);
    public Brush AlternativeBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        if (values.Length > 0 && values[0] is int and > 0) {
            return AlternativeBrush;
        }
        return Brush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}