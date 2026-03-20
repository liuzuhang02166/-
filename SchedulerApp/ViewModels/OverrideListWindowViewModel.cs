using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;

namespace SchedulerApp.ViewModels;

public sealed record OverrideListItem(
    OverrideEntry Entry,
    string DateText,
    string KindText,
    string TeacherText,
    string TimeText,
    string StudentText,
    string ContentText,
    string NoteText,
    string BadgeText
);

public partial class OverrideListWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public ObservableCollection<OverrideListItem> Items { get; } = new();

    [ObservableProperty]
    private OverrideListItem? selectedItem;

    [ObservableProperty]
    private string error = string.Empty;

    public OverrideListWindowViewModel(AppServices services)
    {
        _services = services;
        Reload();
    }

    public void Reload()
    {
        Error = string.Empty;
        Items.Clear();

        var teachers = _services.Teachers.GetAll().ToDictionary(t => t.Id, t => t.Name);
        var courses = _services.Courses.GetAll().ToDictionary(c => c.Id, c => c);

        foreach (var o in _services.Overrides.GetAll())
        {
            courses.TryGetValue(o.CourseId ?? string.Empty, out var src);

            var dateText = $"{o.Date:yyyy.MM.dd}";
            var kindText = o.Kind switch
            {
                OverrideKind.Move => "窜课",
                OverrideKind.Cancel => "停课",
                OverrideKind.Add => "加课",
                _ => o.Kind.ToString()
            };

            var teacherText = o.Kind switch
            {
                OverrideKind.Move => $"{teachers.GetValueOrDefault(o.FromTeacherId ?? src?.TeacherId ?? string.Empty, "未知老师")} → {teachers.GetValueOrDefault(o.ToTeacherId ?? string.Empty, "未知老师")}",
                OverrideKind.Cancel => teachers.GetValueOrDefault(o.FromTeacherId ?? src?.TeacherId ?? string.Empty, "未知老师"),
                OverrideKind.Add => teachers.GetValueOrDefault(o.ToTeacherId ?? string.Empty, "未知老师"),
                _ => string.Empty
            };

            var sm = o.StartMinute ?? src?.StartMinute;
            var em = o.EndMinute ?? src?.EndMinute;
            var timeText = sm is null || em is null ? string.Empty : $"{TimeUtil.FormatMinutes(sm.Value)}-{TimeUtil.FormatMinutes(em.Value)}";

            var studentText = o.StudentName ?? src?.StudentName ?? string.Empty;
            var contentText = o.Content ?? src?.Content ?? string.Empty;
            var noteText = o.Note ?? string.Empty;
            var badge = o.IsForced ? "强制" : string.Empty;

            Items.Add(new OverrideListItem(
                o,
                dateText,
                kindText,
                teacherText,
                timeText,
                studentText,
                contentText,
                noteText,
                badge
            ));
        }

        if (Items.Count > 0)
            SelectedItem = Items[0];
    }
}

