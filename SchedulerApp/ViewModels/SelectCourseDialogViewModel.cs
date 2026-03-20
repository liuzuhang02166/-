using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;

namespace SchedulerApp.ViewModels;

public partial class SelectCourseDialogViewModel : ViewModelBase
{
    private readonly AppServices _services;

    public ObservableCollection<Teacher> Teachers { get; } = new();
    public ObservableCollection<CourseSummary> Courses { get; } = new();

    [ObservableProperty]
    private Teacher? selectedTeacher;

    [ObservableProperty]
    private CourseSummary? selectedCourse;

    [ObservableProperty]
    private string error = string.Empty;

    public SelectCourseDialogViewModel(AppServices services, Teacher? defaultTeacher)
    {
        _services = services;
        foreach (var t in services.Teachers.GetAll())
            Teachers.Add(t);
        SelectedTeacher = defaultTeacher is null ? Teachers.FirstOrDefault() : Teachers.FirstOrDefault(t => t.Id == defaultTeacher.Id);
        ReloadCourses();
    }

    partial void OnSelectedTeacherChanged(Teacher? value)
    {
        ReloadCourses();
    }

    private void ReloadCourses()
    {
        Courses.Clear();
        SelectedCourse = null;
        if (SelectedTeacher is null)
            return;

        foreach (var c in _services.Courses.GetByTeacher(SelectedTeacher.Id))
        {
            var line =
                $"{WeekdayUtil.ToChinese(c.Weekday)} {TimeUtil.FormatMinutes(c.StartMinute)}-{TimeUtil.FormatMinutes(c.EndMinute)}  {c.StudentName}  {c.Content}";
            Courses.Add(new CourseSummary(c.Id, line));
        }
    }
}

