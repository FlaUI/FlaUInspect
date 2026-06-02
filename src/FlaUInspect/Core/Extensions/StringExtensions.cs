namespace FlaUInspect.Core.Extensions;

public static class StringExtensions {
	public static string NormalizeString(this string value) => string.IsNullOrEmpty(value)
			? value
			: value.Replace(Environment.NewLine, " ").Replace('\r', ' ').Replace('\n', ' ');
}