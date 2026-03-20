using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Models;

namespace SchedulerApp.ViewModels;

public sealed record WeekdayOption(int Value, string Text);

public partial class CourseEditorDialogViewModel : ViewModelBase
{
    public string Title { get; }

    [ObservableProperty]
    private string studentName = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private WeekdayOption selectedWeekday;

    [ObservableProperty]
    private DateTimeOffset? startDate;

    [ObservableProperty]
    private DateTimeOffset? endDate;

    [ObservableProperty]
    private bool isOngoing;

    [ObservableProperty]
    private string startTime = "15:00";

    [ObservableProperty]
    private string endTime = "16:00";

    [ObservableProperty]
    private string note = string.Empty;

    [ObservableProperty]
    private string error = string.Empty;

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<WeekdayOption> Weekdays { get; } = new();

    public string? EditingCourseId { get; }
    public bool EndDateEnabled => !IsOngoing;

    public CourseEditorDialogViewModel(string title, string? editingCourseId)
    {
        Title = title;
        EditingCourseId = editingCourseId;
        Weekdays.Add(new WeekdayOption(1, "周一"));
        Weekdays.Add(new WeekdayOption(2, "周二"));
        Weekdays.Add(new WeekdayOption(3, "周三"));
        Weekdays.Add(new WeekdayOption(4, "周四"));
        Weekdays.Add(new WeekdayOption(5, "周五"));
        Weekdays.Add(new WeekdayOption(6, "周六"));
        Weekdays.Add(new WeekdayOption(7, "周日"));
        selectedWeekday = Weekdays[0];
    }

    partial void OnIsOngoingChanged(bool value)
    {
        if (value)
            EndDate = null;
        OnPropertyChanged(nameof(EndDateEnabled));
    }
}
