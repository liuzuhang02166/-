using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Models;
using SchedulerApp.Services;

namespace SchedulerApp.ViewModels;

public partial class WeekNoteEditorDialogViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public DateOnly WeekStart { get; }
    public DateOnly WeekEnd { get; }
    public string WeekRangeText => $"{WeekStart:yyyy.MM.dd} - {WeekEnd:yyyy.MM.dd}";

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private string error = string.Empty;

    public WeekNoteEditorDialogViewModel(AppServices services, DateOnly anchorDate)
    {
        _services = services;
        WeekStart = GetWeekStart(anchorDate);
        WeekEnd = WeekStart.AddDays(6);

        var existing = services.WeekNotes.Get(WeekStart);
        Notes = existing?.Notes ?? string.Empty;
    }

    public void Save()
    {
        Error = string.Empty;
        var note = new WeekNote(WeekStart, Notes ?? string.Empty, DateTimeOffset.UtcNow);
        _services.WeekNotes.Upsert(note);
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
