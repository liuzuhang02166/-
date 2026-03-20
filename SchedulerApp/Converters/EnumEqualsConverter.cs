using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SchedulerApp.Converters;

public sealed class EnumEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        if (value.GetType().IsEnum && parameter is string s)
        {
            if (Enum.TryParse(value.GetType(), s, true, out var parsed))
                return Equals(value, parsed);
        }

        return Equals(value, parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

