using SchedulerApp.Data;
using SchedulerApp.Repositories;

namespace SchedulerApp.Services;

public sealed class AppServices
{
    public AppDb Db { get; }
    public TeacherRepository Teachers { get; }
    public CourseRepository Courses { get; }
    public OverrideRepository Overrides { get; }
    public WeekNoteRepository WeekNotes { get; }

    public AppServices()
    {
        Db = new AppDb(AppPaths.GetDatabasePath());
        Db.EnsureCreated();
        Teachers = new TeacherRepository(Db);
        Courses = new CourseRepository(Db);
        Overrides = new OverrideRepository(Db);
        WeekNotes = new WeekNoteRepository(Db);
    }
}

