using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.Theme;

namespace SchedulerApp.ViewModels;

public sealed record CourseSummary(
    string CourseId,
    string DisplayLine
);

public partial class TeacherScheduleWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly Teacher _teacher;
    private DateOnly _weekStart;

    public string TeacherName => _teacher.Name;
    public Teacher Teacher => _teacher;
    public ObservableCollection<DayScheduleViewModel> WeekDays { get; } = new();

    [ObservableProperty]
    private string weekRangeText = string.Empty;

    public TeacherScheduleWindowViewModel(AppServices services, Teacher teacher)
    {
        _services = services;
        _teacher = teacher;
        _weekStart = GetWeekStart(DateOnly.FromDateTime(DateTime.Today));
        ReloadWeekSchedule();
    }

    public void GoPrevWeek()
    {
        _weekStart = _weekStart.AddDays(-7);
        ReloadWeekSchedule();
    }

    public void GoNextWeek()
    {
        _weekStart = _weekStart.AddDays(7);
        ReloadWeekSchedule();
    }

    public void GoToWeek(DateOnly anyDateInWeek)
    {
        _weekStart = GetWeekStart(anyDateInWeek);
        ReloadWeekSchedule();
    }

    public void ReloadWeekSchedule()
    {
        var weekEnd = _weekStart.AddDays(6);
        WeekRangeText = $"{_weekStart:yyyy.MM.dd} - {weekEnd:yyyy.MM.dd}";

        var teachers = _services.Teachers.GetAll().ToDictionary(t => t.Id, t => t);

        var courses = _services.Courses.GetAll();
        var coursesById = courses.ToDictionary(c => c.Id, c => c);
        var overrides = _services.Overrides.GetByDateRange(_weekStart, weekEnd)
            .GroupBy(o => o.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        WeekDays.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = _weekStart.AddDays(i);
            var weekday = i + 1;
            var title = $"{date:MM.dd} {WeekdayUtil.ToChinese(weekday)}";
            var day = new DayScheduleViewModel(title);

            var baseItems = courses
                .Where(c => c.TeacherId == _teacher.Id)
                .Where(c => c.Weekday == weekday)
                .Where(c => date >= c.StartDate)
                .Where(c => c.EndDate is null || date <= c.EndDate.Value)
                .OrderBy(c => c.StartMinute)
                .ThenBy(c => c.StudentName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var occ = baseItems
                .Select(c =>
                {
                    teachers.TryGetValue(c.TeacherId, out var t);
                    var sw = TeacherColorPalette.Get(c.TeacherId, t?.ColorHex);
                    return new Occurrence(
                        c.Id,
                        c.TeacherId,
                        t?.Name ?? "未知老师",
                        c.StudentName,
                        c.Content,
                        c.StartMinute,
                        c.EndMinute,
                        c.Note,
                        string.Empty,
                        sw.BackgroundBrush,
                        sw.BorderBrush
                    );
                })
                .ToList();

            if (overrides.TryGetValue(date, out var dayOverrides))
            {
                foreach (var o in dayOverrides.OrderBy(x => x.StartMinute ?? int.MaxValue))
                {
                    if (o.Kind == OverrideKind.Cancel && o.CourseId is not null)
                    {
                        occ.RemoveAll(x => x.CourseId == o.CourseId);
                        continue;
                    }

                    if (o.Kind == OverrideKind.Move && o.CourseId is not null)
                    {
                        occ.RemoveAll(x => x.CourseId == o.CourseId);

                        coursesById.TryGetValue(o.CourseId, out var src);
                        var toTeacherId = o.ToTeacherId ?? src?.TeacherId ?? string.Empty;
                        if (toTeacherId != _teacher.Id)
                            continue;

                        var toTeacherName = teachers.TryGetValue(toTeacherId, out var n) ? n.Name : "未知老师";
                        var sm = o.StartMinute ?? src?.StartMinute ?? 0;
                        var em = o.EndMinute ?? src?.EndMinute ?? 0;
                        var badge = o.IsForced ? "窜课·冲突" : "窜课";
                        var sw = TeacherColorPalette.Get(toTeacherId, teachers.TryGetValue(toTeacherId, out var tt) ? tt.ColorHex : null);
                        occ.Add(new Occurrence(
                            o.CourseId,
                            toTeacherId,
                            toTeacherName,
                            o.StudentName ?? src?.StudentName ?? string.Empty,
                            o.Content ?? src?.Content ?? string.Empty,
                            sm,
                            em,
                            o.Note,
                            badge,
                            sw.BackgroundBrush,
                            o.IsForced ? new SolidColorBrush(Color.FromRgb(252, 165, 165)) : sw.BorderBrush
                        ));
                        continue;
                    }

                    if (o.Kind == OverrideKind.Add)
                    {
                        var teacherId = o.ToTeacherId ?? o.FromTeacherId ?? string.Empty;
                        if (teacherId != _teacher.Id)
                            continue;

                        var teacherName = teachers.TryGetValue(teacherId, out var n) ? n.Name : "未知老师";
                        var sm = o.StartMinute ?? 0;
                        var em = o.EndMinute ?? 0;
                        var badge = o.IsForced ? "加课·冲突" : "加课";
                        var sw = TeacherColorPalette.Get(teacherId, teachers.TryGetValue(teacherId, out var tt) ? tt.ColorHex : null);
                        occ.Add(new Occurrence(
                            o.Id,
                            teacherId,
                            teacherName,
                            o.StudentName ?? string.Empty,
                            o.Content ?? string.Empty,
                            sm,
                            em,
                            o.Note,
                            badge,
                            sw.BackgroundBrush,
                            o.IsForced ? new SolidColorBrush(Color.FromRgb(252, 165, 165)) : sw.BorderBrush
                        ));
                    }
                }
            }

            foreach (var c in occ.OrderBy(x => x.StartMinute).ThenBy(x => x.StudentName, StringComparer.OrdinalIgnoreCase))
            {
                day.Items.Add(new ScheduleOccurrenceViewModel(
                    c.CourseId,
                    c.TeacherName,
                    c.StudentName,
                    c.Content,
                    $"{TimeUtil.FormatMinutes(c.StartMinute)}-{TimeUtil.FormatMinutes(c.EndMinute)}",
                    c.Note,
                    c.Badge,
                    c.Background,
                    c.BorderBrush
                ));
            }

            WeekDays.Add(day);
        }
    }

    private sealed record Occurrence(
        string CourseId,
        string TeacherId,
        string TeacherName,
        string StudentName,
        string Content,
        int StartMinute,
        int EndMinute,
        string Note,
        string Badge,
        IBrush Background,
        IBrush BorderBrush
    );

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var weekday = ToWeekday1To7(date);
        return date.AddDays(-(weekday - 1));
    }

    private static int ToWeekday1To7(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return dow == 0 ? 7 : dow;
    }
}
