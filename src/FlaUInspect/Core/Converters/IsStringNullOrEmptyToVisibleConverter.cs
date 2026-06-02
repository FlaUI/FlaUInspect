using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class IsStringNullOrEmptyToVisibleConverter : IValueConverter {
	public bool IsInverted { get; set; }

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !string.IsNullOrEmpty(value as string) | IsInverted // If string is not null or empty, (bitwise)or if inverted
		? Visibility.Visible
		: Visibility.Collapsed;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}