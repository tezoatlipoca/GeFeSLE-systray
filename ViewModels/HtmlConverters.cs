using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeFeSLE.ViewModels
{
    public class AllTrueConverter : IMultiValueConverter
    {
        public static readonly AllTrueConverter Instance = new AllTrueConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count == 0)
                return false;

            return values.All(value => value is bool boolValue && boolValue);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class CountToZeroConverter : IValueConverter
    {
        public static readonly CountToZeroConverter Instance = new CountToZeroConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count)
                return count == 0;
            
            return true; // If not an int, assume zero/empty
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
