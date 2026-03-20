using System;

namespace SchedulerApp.Domain;

public static class TimeUtil
{
    public static string FormatMinutes(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        return $"{h:00}:{m:00}";
    }

    public static int ParseHhMm(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new FormatException("时间格式应为 HH:MM。");
        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }
}

