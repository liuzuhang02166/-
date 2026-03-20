using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;

namespace SchedulerApp.ViewModels;

public sealed record OverrideActionOption(OverrideKind Kind, string Text);
public sealed record CourseOccurrenceOption(string CourseId, string DisplayLine);
public sealed record TimeSlotOption(
    int StartMinute,
    int EndMinute,
    bool IsFree,
    string DisplayLine,
    string BadgeText,
    IBrush BadgeBackground,
    IBrush BadgeBorder
);

public partial class OverrideEditorDialogViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly OverrideEntry? _editingEntry;

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<OverrideActionOption> Actions { get; } = new();
    public ObservableCollection<CourseOccurrenceOption> CandidateCourses { get; } = new();
    public ObservableCollection<TimeSlotOption> TimeSlots { get; } = new();

    [ObservableProperty]
    private DateTimeOffset? selectedDate;

    [ObservableProperty]
    private OverrideActionOption selectedAction;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private Teacher? targetTeacher;

    [ObservableProperty]
    private CourseOccurrenceOption? selectedCourse;

    [ObservableProperty]
    private string studentName = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private string startTime = "11:00";

    [ObservableProperty]
    private string endTime = "12:00";

    [ObservableProperty]
    private string note = string.Empty;

    [ObservableProperty]
    private string error = string.Empty;

    [ObservableProperty]
    private TimeSlotOption? selectedTimeSlot;

    public string? EditingOverrideId => _editingEntry?.Id;
    public DateTimeOffset? EditingCreatedAt => _editingEntry?.CreatedAt;

    public OverrideEditorDialogViewModel(AppServices services, Teacher? defaultTeacher, OverrideEntry? editingEntry = null)
    {
        _services = services;
        _editingEntry = editingEntry;

        foreach (var t in services.Teachers.GetAll())
            Teachers.Add(t);

        Actions.Add(new OverrideActionOption(OverrideKind.Move, "窜课（换老师/换时间）"));
        Actions.Add(new OverrideActionOption(OverrideKind.Cancel, "停课（当日取消）"));
        Actions.Add(new OverrideActionOption(OverrideKind.Add, "加课（当日新增）"));
        selectedAction = Actions[0];

        SelectedTeacher = defaultTeacher is null ? Teachers.FirstOrDefault() : Teachers.FirstOrDefault(t => t.Id == defaultTeacher.Id);
        TargetTeacher = SelectedTeacher;

        SelectedDate = new DateTimeOffset(DateTime.Today);

        if (editingEntry is not null)
        {
            SelectedDate = new DateTimeOffset(editingEntry.Date.ToDateTime(TimeOnly.MinValue));
            SelectedAction = Actions.First(x => x.Kind == editingEntry.Kind);

            var course = editingEntry.CourseId is null ? null : services.Courses.GetById(editingEntry.CourseId);
            var fromTeacherId = editingEntry.FromTeacherId ?? course?.TeacherId;
            var toTeacherId = editingEntry.ToTeacherId;

            if (fromTeacherId is not null)
                SelectedTeacher = Teachers.FirstOrDefault(t => t.Id == fromTeacherId) ?? SelectedTeacher;

            if (editingEntry.Kind == OverrideKind.Move)
            {
                TargetTeacher = Teachers.FirstOrDefault(t => t.Id == (toTeacherId ?? SelectedTeacher?.Id)) ?? SelectedTeacher;
                var sm = editingEntry.StartMinute ?? course?.StartMinute;
                var em = editingEntry.EndMinute ?? course?.EndMinute;
                if (sm is not null)
                    StartTime = TimeUtil.FormatMinutes(sm.Value);
                if (em is not null)
                    EndTime = TimeUtil.FormatMinutes(em.Value);
            }

            if (editingEntry.Kind == OverrideKind.Add)
            {
                TargetTeacher = Teachers.FirstOrDefault(t => t.Id == toTeacherId) ?? SelectedTeacher;
                StudentName = editingEntry.StudentName ?? string.Empty;
                Content = editingEntry.Content ?? string.Empty;
                if (editingEntry.StartMinute is not null)
                    StartTime = TimeUtil.FormatMinutes(editingEntry.StartMinute.Value);
                if (editingEntry.EndMinute is not null)
                    EndTime = TimeUtil.FormatMinutes(editingEntry.EndMinute.Value);
            }

            Note = editingEntry.Note ?? string.Empty;
        }

        ReloadCandidateCourses();
        if (editingEntry is not null && editingEntry.Kind != OverrideKind.Add && editingEntry.CourseId is not null)
        {
            var found = CandidateCourses.FirstOrDefault(x => x.CourseId == editingEntry.CourseId);
            if (found is null)
            {
                var line =
                    $"（原课程）{editingEntry.StudentName ?? string.Empty}  {editingEntry.Content ?? string.Empty}".Trim();
                CandidateCourses.Insert(0, new CourseOccurrenceOption(editingEntry.CourseId, line));
                SelectedCourse = CandidateCourses[0];
            }
            else
            {
                SelectedCourse = found;
            }
        }
        ReloadTimeSlots();
    }

    public DateOnly? GetSelectedDateOnly()
    {
        if (SelectedDate is null)
            return null;
        return DateOnly.FromDateTime(SelectedDate.Value.Date);
    }

    public int? GetSelectedWeekday()
    {
        var d = GetSelectedDateOnly();
        if (d is null)
            return null;
        return ToWeekday1To7(d.Value);
    }

    partial void OnSelectedDateChanged(DateTimeOffset? value)
    {
        ReloadCandidateCourses();
        ReloadTimeSlots();
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        if (TargetTeacher is null)
            TargetTeacher = value;
        ReloadCandidateCourses();
    }

    partial void OnTargetTeacherChanged(Teacher? value)
    {
        ReloadTimeSlots();
    }

    partial void OnSelectedActionChanged(OverrideActionOption value)
    {
        ReloadCandidateCourses();
        ReloadTimeSlots();
    }

    partial void OnSelectedTimeSlotChanged(TimeSlotOption? value)
    {
        if (value is null || !value.IsFree)
            return;
        StartTime = TimeUtil.FormatMinutes(value.StartMinute);
        EndTime = TimeUtil.FormatMinutes(value.EndMinute);
    }

    private void ReloadCandidateCourses()
    {
        CandidateCourses.Clear();
        SelectedCourse = null;

        if (SelectedTeacher is null)
            return;

        var date = GetSelectedDateOnly();
        if (date is null)
            return;

        var weekday = ToWeekday1To7(date.Value);
        if (SelectedAction.Kind == OverrideKind.Add)
            return;

        var courses = _services.Courses.GetByTeacher(SelectedTeacher.Id)
            .Where(c => c.Weekday == weekday)
            .Where(c => date.Value >= c.StartDate)
            .Where(c => c.EndDate is null || date.Value <= c.EndDate.Value)
            .OrderBy(c => c.StartMinute)
            .ThenBy(c => c.StudentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var c in courses)
        {
            var line =
                $"{TimeUtil.FormatMinutes(c.StartMinute)}-{TimeUtil.FormatMinutes(c.EndMinute)}  {c.StudentName}  {c.Content}";
            CandidateCourses.Add(new CourseOccurrenceOption(c.Id, line));
        }

        if (CandidateCourses.Count > 0)
            SelectedCourse = CandidateCourses[0];
    }

    private void ReloadTimeSlots()
    {
        TimeSlots.Clear();
        SelectedTimeSlot = null;

        var date = GetSelectedDateOnly();
        if (date is null)
            return;
        if (TargetTeacher is null)
            return;

        var busy = GetBusyIntervals(TargetTeacher.Id, date.Value, null);
        var dayStart = 8 * 60;
        var dayEnd = 20 * 60;
        busy = busy
            .Where(x => x.End > dayStart && x.Start < dayEnd)
            .Select(x => new Interval(Math.Max(dayStart, x.Start), Math.Min(dayEnd, x.End)))
            .OrderBy(x => x.Start)
            .ToList();

        var merged = new ObservableCollection<Interval>();
        foreach (var b in busy)
        {
            if (merged.Count == 0)
            {
                merged.Add(b);
                continue;
            }
            var last = merged[^1];
            if (b.Start <= last.End)
                merged[^1] = new Interval(last.Start, Math.Max(last.End, b.End));
            else
                merged.Add(b);
        }

        var cursor = dayStart;
        foreach (var b in merged)
        {
            if (cursor < b.Start)
                AddSlot(cursor, b.Start, true);
            AddSlot(b.Start, b.End, false);
            cursor = b.End;
        }
        if (cursor < dayEnd)
            AddSlot(cursor, dayEnd, true);
    }

    private void AddSlot(int startMinute, int endMinute, bool isFree)
    {
        if (endMinute <= startMinute)
            return;
        var badgeText = isFree ? "空" : "占用";
        var bg = isFree ? Brush.Parse("#DCFCE7") : Brush.Parse("#FEE2E2");
        var bd = isFree ? Brush.Parse("#86EFAC") : Brush.Parse("#FCA5A5");
        TimeSlots.Add(new TimeSlotOption(
            startMinute,
            endMinute,
            isFree,
            $"{TimeUtil.FormatMinutes(startMinute)} - {TimeUtil.FormatMinutes(endMinute)}",
            badgeText,
            bg,
            bd
        ));
    }

    public bool WouldConflict(string teacherId, DateOnly date, int startMinute, int endMinute, string? ignoreCourseId)
    {
        var baseOcc = GetBusyIntervals(teacherId, date, ignoreCourseId);
        return baseOcc.Any(x => Overlaps(x.Start, x.End, startMinute, endMinute));
    }

    private sealed record Interval(int Start, int End);

    private static bool Overlaps(int aStart, int aEnd, int bStart, int bEnd)
    {
        return aStart < bEnd && bStart < aEnd;
    }

    private IReadOnlyList<Interval> GetBusyIntervals(string teacherId, DateOnly date, string? ignoreCourseId)
    {
        var weekday = ToWeekday1To7(date);

        var allCourses = _services.Courses.GetAll();
        var baseOcc = allCourses
            .Where(c => c.TeacherId == teacherId)
            .Where(c => c.Weekday == weekday)
            .Where(c => date >= c.StartDate)
            .Where(c => c.EndDate is null || date <= c.EndDate.Value)
            .Where(c => ignoreCourseId is null || c.Id != ignoreCourseId)
            .Select(c => new Interval(c.StartMinute, c.EndMinute))
            .ToList();

        var dayOverrides = _services.Overrides.GetByDateRange(date, date);
        foreach (var o in dayOverrides)
        {
            if (o.Kind == OverrideKind.Cancel && o.CourseId is not null)
            {
                var src = _services.Courses.GetById(o.CourseId);
                if (src is not null && src.TeacherId == teacherId)
                    baseOcc.RemoveAll(x => x.Start == src.StartMinute && x.End == src.EndMinute);
                continue;
            }

            if (o.Kind == OverrideKind.Move && o.CourseId is not null)
            {
                var src = _services.Courses.GetById(o.CourseId);
                if (src is not null && src.TeacherId == teacherId)
                    baseOcc.RemoveAll(x => x.Start == src.StartMinute && x.End == src.EndMinute);
                var toTeacherId = o.ToTeacherId;
                if (toTeacherId is null || toTeacherId != teacherId)
                    continue;
                if (ignoreCourseId is not null && o.CourseId == ignoreCourseId)
                    continue;
                if (o.StartMinute is null || o.EndMinute is null)
                    continue;
                baseOcc.Add(new Interval(o.StartMinute.Value, o.EndMinute.Value));
                continue;
            }

            if (o.Kind == OverrideKind.Add)
            {
                var t = o.ToTeacherId ?? o.FromTeacherId;
                if (t is null || t != teacherId)
                    continue;
                if (o.StartMinute is null || o.EndMinute is null)
                    continue;
                baseOcc.Add(new Interval(o.StartMinute.Value, o.EndMinute.Value));
            }
        }

        return baseOcc;
    }

    private static int ToWeekday1To7(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return dow == 0 ? 7 : dow;
    }
}
