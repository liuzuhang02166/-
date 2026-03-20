using Avalonia.Media;
using System.Collections.ObjectModel;

namespace SchedulerApp.ViewModels;

public sealed record ScheduleOccurrenceViewModel(
    string CourseId,
    string TeacherName,
    string StudentName,
    string Content,
    string TimeRange,
    string Note,
    string Badge,
    IBrush Background,
    IBrush BorderBrush
);

public sealed class DayScheduleViewModel : ViewModelBase
{
    public string DateText { get; }
    public ObservableCollection<ScheduleOccurrenceViewModel> Items { get; } = new();

    public DayScheduleViewModel(string dateText)
    {
        DateText = dateText;
    }
}
