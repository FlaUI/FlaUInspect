using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class CountToVisibilityConverter : IValueConverter {
	public Visibility ZeroCountVisibility { get; set; } = Visibility.Collapsed;
	public Visibility MultipleCountVisibility { get; set; } = Visibility.Visible;

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int count && count > 0 ? MultipleCountVisibility : ZeroCountVisibility;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}