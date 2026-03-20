using System;
using System.IO;

namespace SchedulerApp.Data;

public static class AppPaths
{
    public static string GetAppDataDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(baseDir, "SchedulerApp");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetDatabasePath()
    {
        return Path.Combine(GetAppDataDirectory(), "scheduler.db");
    }
}

