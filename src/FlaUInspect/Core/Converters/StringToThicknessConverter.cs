using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlaUInspect.Core.Converters;

public class StringToThicknessConverter : IValueConverter {
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is string str
		&& !string.IsNullOrWhiteSpace(str)
		&& str.Split([',', ' ']) is string[] parts
			? parts.Length == 4
			&& double.TryParse(parts[0], out var left)
			&& double.TryParse(parts[1], out var top)
			&& double.TryParse(parts[2], out var right)
			&& double.TryParse(parts[3], out var bottom)
				? new Thickness(left, top, right, bottom)
				: parts.Length == 2
				&& double.TryParse(parts[0], out var leftRigth)
				&& double.TryParse(parts[1], out var topBottom)
					? new Thickness(leftRigth, topBottom, leftRigth, topBottom)
					: new Thickness(0)
			: new Thickness(0);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is Thickness thickness ? $"{thickness.Left},{thickness.Top},{thickness.Right},{thickness.Bottom}" : string.Empty;
}