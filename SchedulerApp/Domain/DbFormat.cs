using System;
using System.Globalization;

namespace SchedulerApp.Domain;

public static class DbFormat
{
    public const string DateFormat = "yyyy-MM-dd";

    public static string ToDbDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ParseDbDate(string value) =>
        DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);
}

