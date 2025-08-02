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

public class BoolToAddEditTextConverter : IValueConverter
{
    public static readonly BoolToAddEditTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAdding)
        {
            return isAdding ? "Add" : "Edit";
        }
        return "Edit";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToSaveButtonTextConverter : IValueConverter
{
    public static readonly BoolToSaveButtonTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAdding)
        {
            return isAdding ? "Add Item" : "Save Changes";
        }
        return "Save Changes";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToChangesNewItemTextConverter : IValueConverter
{
    public static readonly BoolToChangesNewItemTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAdding)
        {
            return isAdding ? "New item" : "Changes";
        }
        return "Changes";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
