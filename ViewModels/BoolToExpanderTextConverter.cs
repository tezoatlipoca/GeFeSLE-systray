using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GeFeSLE.ViewModels;

public class BoolToExpanderTextConverter : IValueConverter
{
    public static readonly BoolToExpanderTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "▼ Collapse" : "▶ Expand";
        }
        return "▶ Expand";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
