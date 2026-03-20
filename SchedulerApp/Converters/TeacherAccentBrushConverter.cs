using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SchedulerApp.Models;
using SchedulerApp.Theme;

namespace SchedulerApp.Converters;

public sealed class TeacherAccentBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return Brushes.Transparent;

        if (value is Teacher t)
            return TeacherColorPalette.Get(t.Id, t.ColorHex).BaseBrush;

        var id = value as string ?? value.ToString() ?? string.Empty;
        return TeacherColorPalette.Get(id).BaseBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
