using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Models;
using SchedulerApp.Services;

namespace SchedulerApp.ViewModels;

public enum ExportDateMode
{
    ThisWeek = 0,
    NextWeek = 1,
    Custom = 2
}

public partial class ExportPreviewDialogViewModel : ViewModelBase
{
    private readonly AppServices _services;

    private readonly DateOnly _anchorWeekStart;
    public DateOnly ThisWeekStart => _anchorWeekStart;
    public DateOnly NextWeekStart => _anchorWeekStart.AddDays(7);

    public DateOnly RangeStart => DateMode switch
    {
        ExportDateMode.ThisWeek => ThisWeekStart,
        ExportDateMode.NextWeek => NextWeekStart,
        _ => GetCustomStartOrDefault()
    };

    public DateOnly RangeEnd => DateMode switch
    {
        ExportDateMode.ThisWeek => ThisWeekStart.AddDays(6),
        ExportDateMode.NextWeek => NextWeekStart.AddDays(6),
        _ => GetCustomEndOrDefault()
    };

    public string DateRangeText => $"{RangeStart:yyyy.MM.dd} - {RangeEnd:yyyy.MM.dd}";
    public bool IncludeWeekNotesEnabled => IsSingleWeekRange(RangeStart, RangeEnd);

    public ObservableCollection<Teacher> Teachers { get; } = new();

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private bool exportAllTeachers = true;

    [ObservableProperty]
    private ExportDateMode dateMode = ExportDateMode.ThisWeek;

    [ObservableProperty]
    private DateTimeOffset? customStartDate;

    [ObservableProperty]
    private DateTimeOffset? customEndDate;

    [ObservableProperty]
    private bool includeWeekNotes;

    [ObservableProperty]
    private string error = string.Empty;

    public ExportPreviewDialogViewModel(AppServices services, DateOnly anchorDate, Teacher? defaultTeacher)
    {
        _services = services;
        _anchorWeekStart = GetWeekStart(anchorDate);
        CustomStartDate = new DateTimeOffset(ThisWeekStart.ToDateTime(TimeOnly.MinValue));
        CustomEndDate = new DateTimeOffset(ThisWeekStart.AddDays(6).ToDateTime(TimeOnly.MinValue));

        foreach (var t in services.Teachers.GetAll())
            Teachers.Add(t);

        SelectedTeacher = defaultTeacher is null ? Teachers.FirstOrDefault() : Teachers.FirstOrDefault(t => t.Id == defaultTeacher.Id);
        ExportAllTeachers = true;
        DateMode = ExportDateMode.ThisWeek;
        IncludeWeekNotes = false;
    }

    public string? GetTeacherIdFilter()
    {
        if (ExportAllTeachers)
            return null;
        return SelectedTeacher?.Id;
    }

    public bool TryGetSelectedRange(out DateOnly start, out DateOnly end, out string errorMessage)
    {
        start = RangeStart;
        end = RangeEnd;

        if (end < start)
        {
            errorMessage = "结束日期不能早于开始日期。";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    partial void OnDateModeChanged(ExportDateMode value)
    {
        OnPropertyChanged(nameof(RangeStart));
        OnPropertyChanged(nameof(RangeEnd));
        OnPropertyChanged(nameof(DateRangeText));
        OnPropertyChanged(nameof(IncludeWeekNotesEnabled));
        if (!IncludeWeekNotesEnabled)
            IncludeWeekNotes = false;
    }

    partial void OnCustomStartDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(RangeStart));
        OnPropertyChanged(nameof(DateRangeText));
        OnPropertyChanged(nameof(IncludeWeekNotesEnabled));
        if (!IncludeWeekNotesEnabled)
            IncludeWeekNotes = false;
    }

    partial void OnCustomEndDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(RangeEnd));
        OnPropertyChanged(nameof(DateRangeText));
        OnPropertyChanged(nameof(IncludeWeekNotesEnabled));
        if (!IncludeWeekNotesEnabled)
            IncludeWeekNotes = false;
    }

    private DateOnly GetCustomStartOrDefault()
    {
        if (CustomStartDate is null)
            return ThisWeekStart;
        return DateOnly.FromDateTime(CustomStartDate.Value.Date);
    }

    private DateOnly GetCustomEndOrDefault()
    {
        if (CustomEndDate is null)
            return ThisWeekStart.AddDays(6);
        return DateOnly.FromDateTime(CustomEndDate.Value.Date);
    }

    private static bool IsSingleWeekRange(DateOnly start, DateOnly end)
    {
        if (end != start.AddDays(6))
            return false;
        return ToWeekday1To7(start) == 1;
    }

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
