using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class CourseEditorWindow : Window
{
    private readonly AppServices _services;
    private readonly Course? _editing;
    public Course? ResultCourse { get; private set; }

    public CourseEditorWindow(AppServices services, Course? editing, Teacher? defaultTeacher)
    {
        _services = services;
        _editing = editing;

        InitializeComponent();

        var vm = new CourseEditorDialogViewModel(editing is null ? "添加学员" : "更改学员", editing?.Id);
        foreach (var t in services.Teachers.GetAll())
            vm.Teachers.Add(t);

        if (editing is not null)
        {
            vm.StudentName = editing.StudentName;
            vm.Content = editing.Content;
            vm.Note = editing.Note;
            vm.SelectedTeacher = vm.Teachers.FirstOrDefault(t => t.Id == editing.TeacherId);
            vm.SelectedWeekday = vm.Weekdays.FirstOrDefault(w => w.Value == editing.Weekday) ?? vm.Weekdays[0];
            vm.StartDate = new DateTimeOffset(editing.StartDate.ToDateTime(TimeOnly.MinValue));
            vm.IsOngoing = editing.EndDate is null;
            vm.EndDate = editing.EndDate is null ? null : new DateTimeOffset(editing.EndDate.Value.ToDateTime(TimeOnly.MinValue));
            vm.StartTime = TimeUtil.FormatMinutes(editing.StartMinute);
            vm.EndTime = TimeUtil.FormatMinutes(editing.EndMinute);
        }
        else
        {
            vm.StartDate = new DateTimeOffset(DateTime.Today);
            vm.IsOngoing = true;
            vm.SelectedTeacher = defaultTeacher is null ? vm.Teachers.FirstOrDefault() : vm.Teachers.FirstOrDefault(t => t.Id == defaultTeacher.Id);
        }

        DataContext = vm;

        OkButton.Click += OkButtonOnClick;
        CancelButton.Click += (_, _) => Close();
    }

    private void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CourseEditorDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(vm.StudentName))
                throw new InvalidOperationException("学员名称不能为空。");
            if (string.IsNullOrWhiteSpace(vm.Content))
                throw new InvalidOperationException("教学内容不能为空。");
            if (vm.SelectedTeacher is null)
                throw new InvalidOperationException("请选择教学老师。");
            if (vm.StartDate is null)
                throw new InvalidOperationException("请选择开始日期。");

            var startMinute = TimeUtil.ParseHhMm(vm.StartTime);
            var endMinute = TimeUtil.ParseHhMm(vm.EndTime);
            if (endMinute <= startMinute)
                throw new InvalidOperationException("上课结束时间必须晚于开始时间。");

            var startDate = DateOnly.FromDateTime(vm.StartDate.Value.Date);
            DateOnly? endDate = null;
            if (!vm.IsOngoing)
            {
                if (vm.EndDate is null)
                    throw new InvalidOperationException("请选择终止日期，或勾选“持续”。");
                endDate = DateOnly.FromDateTime(vm.EndDate.Value.Date);
                if (endDate.Value < startDate)
                    throw new InvalidOperationException("终止日期不能早于开始日期。");
                var maxYear = DateTime.Today.Year + 5;
                if (endDate.Value.Year > maxYear)
                    throw new InvalidOperationException("终止年份最大为当年的后5年。");
            }

            var weekday = vm.SelectedWeekday.Value;
            var conflict = _services.Courses.FindConflictingCourse(
                vm.SelectedTeacher.Id,
                weekday,
                startMinute,
                endMinute,
                vm.EditingCourseId);
            if (conflict is not null)
                throw new InvalidOperationException($"与 {conflict.StudentName} 课程冲突");

            if (_editing is null)
            {
                var course = new Course(
                    Guid.NewGuid().ToString("N"),
                    vm.StudentName.Trim(),
                    vm.Content.Trim(),
                    vm.SelectedTeacher.Id,
                    startDate,
                    endDate,
                    weekday,
                    startMinute,
                    endMinute,
                    vm.Note ?? string.Empty,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow
                );
                ResultCourse = _services.Courses.Add(course);
            }
            else
            {
                var updated = _editing with
                {
                    StudentName = vm.StudentName.Trim(),
                    Content = vm.Content.Trim(),
                    TeacherId = vm.SelectedTeacher.Id,
                    StartDate = startDate,
                    EndDate = endDate,
                    Weekday = weekday,
                    StartMinute = startMinute,
                    EndMinute = endMinute,
                    Note = vm.Note ?? string.Empty
                };
                ResultCourse = _services.Courses.Update(updated);
            }

            Close(ResultCourse);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}
