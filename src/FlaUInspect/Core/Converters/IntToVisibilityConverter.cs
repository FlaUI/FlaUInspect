using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NotSupportedException = FlaUI.Core.Exceptions.NotSupportedException;

namespace FlaUInspect.Core.Converters;

public class IntToVisibilityConverter : IValueConverter {
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is int intValue
		&& ((parameter is "EqualsZero" && intValue == 0)
			|| (parameter is "GreaterThanZero" && intValue > 0))
				? Visibility.Visible
				: Visibility.Collapsed;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}