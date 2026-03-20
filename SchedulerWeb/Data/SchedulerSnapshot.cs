using System.Collections.Generic;
using SchedulerApp.Models;

namespace SchedulerWeb.Data;

public sealed record SchedulerSnapshot(
    List<Teacher> Teachers,
    List<Course> Courses,
    List<OverrideEntry> Overrides,
    List<WeekNote> WeekNotes
);

