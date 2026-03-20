﻿﻿﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.Theme;

namespace SchedulerApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private DateOnly _weekStart;

    [ObservableProperty]
    private string teacherQuery = string.Empty;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<Teacher> FilteredTeachers { get; } = new();
    public ObservableCollection<DayScheduleViewModel> WeekDays { get; } = new();

    [ObservableProperty]
    private string weekRangeText = string.Empty;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;
        _weekStart = GetWeekStart(DateOnly.FromDateTime(DateTime.Today));
        ReloadTeachers();
        ReloadWeekSchedule();
    }

    public void ReloadTeachers()
    {
        Teachers.Clear();
        foreach (var teacher in _services.Teachers.GetAll())
            Teachers.Add(teacher);

        ApplyTeacherFilter();
    }

    partial void OnTeacherQueryChanged(string value)
    {
        ApplyTeacherFilter();
    }

    private void ApplyTeacherFilter()
    {
        var selectedId = SelectedTeacher?.Id;
        var q = TeacherQuery.Trim();
        var items = string.IsNullOrWhiteSpace(q)
            ? Teachers.ToList()
            : Teachers.Where(t => t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredTeachers.Clear();
        foreach (var item in items)
            FilteredTeachers.Add(item);

        if (selectedId is not null)
            SelectedTeacher = FilteredTeachers.FirstOrDefault(t => t.Id == selectedId);

        if (SelectedTeacher is null && FilteredTeachers.Count > 0)
            SelectedTeacher = FilteredTeachers[0];
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
