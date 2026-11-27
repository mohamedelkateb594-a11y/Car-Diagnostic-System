using System.Globalization;
using System.Windows.Data;
using Diagnostic.Desktop.Services;

namespace Diagnostic.Desktop.Converters;

/// <summary>
/// MultiBinding converter: values[0] is the string key; values[1] (LocalizationService.Revision)
/// only exists so the binding is re-evaluated when the language changes.
/// </summary>
public sealed class LocalizeConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = values.Length > 0 ? values[0] as string : null;
        return string.IsNullOrEmpty(key) ? string.Empty : LocalizationService.Instance[key];
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
