using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        RefreshButton.Click += RefreshButtonOnClick;
        AddTeacherMenuButton.Click += AddTeacherButtonOnClick;
        DeleteTeacherMenuButton.Click += DeleteTeacherMenuButtonOnClick;
        OpenTeacherScheduleButton.Click += OpenTeacherScheduleButtonOnClick;
        TeachersListBox.DoubleTapped += (_, _) => _ = OpenTeacherScheduleAsync();
        AddCourseMenuButton.Click += AddCourseButtonOnClick;
        EditCourseMenuButton.Click += EditCourseButtonOnClick;
        DeleteCourseMenuButton.Click += DeleteCourseButtonOnClick;
        AddOverrideMenuButton.Click += AddOverrideMenuButtonOnClick;
        ExistingOverridesMenuButton.Click += ExistingOverridesMenuButtonOnClick;
        NoticeButton.Click += NoticeButtonOnClick;
        ExportButton.Click += ExportButtonOnClick;
        PrevWeekButton.Click += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.GoPrevWeek();
        };
        NextWeekButton.Click += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.GoNextWeek();
        };
        ThisWeekButton.Click += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.GoToWeek(DateOnly.FromDateTime(DateTime.Today));
        };
    }

    private void RefreshButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.ReloadTeachers();
        vm.ReloadWeekSchedule();
    }

    private async void AddOverrideMenuButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var w = new OverrideEditorWindow(App.Services, vm.SelectedTeacher);
        var ok = await w.ShowDialog<bool>(this);
        if (ok)
            vm.ReloadWeekSchedule();
    }

    private async void ExistingOverridesMenuButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var w = new OverrideListWindow(App.Services, vm.SelectedTeacher);
        var ok = await w.ShowDialog<bool>(this);
        if (ok)
            vm.ReloadWeekSchedule();
    }

    private async void NoticeButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var w = new WeekNoteEditorWindow(App.Services, DateOnly.FromDateTime(DateTime.Today));
        var ok = await w.ShowDialog<bool>(this);
        if (ok)
            vm.ReloadWeekSchedule();
    }

    private async void ExportButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var w = new ExportPreviewWindow(App.Services, DateOnly.FromDateTime(DateTime.Today), vm.SelectedTeacher);
        await w.ShowDialog(this);
    }

    private async void AddTeacherButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var dlg = new AddTeacherWindow(App.Services);
        await dlg.ShowDialog(this);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ReloadTeachers();
            vm.ReloadWeekSchedule();
        }
    }

    private async void DeleteTeacherMenuButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        if (vm.SelectedTeacher is null)
            return;

        var courseCount = App.Services.Courses.CountByTeacherId(vm.SelectedTeacher.Id);
        var message = courseCount == 0
            ? $"确认删除老师：{vm.SelectedTeacher.Name}？"
            : $"老师：{vm.SelectedTeacher.Name}\n当前还有 {courseCount} 个学员排课。\n\n确认删除老师，并同时删除其所有学员排课？";

        var confirm = new ConfirmWindow("删除老师确认", message, "删除", "取消");
        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok)
            return;

        if (courseCount > 0)
        {
            var confirm2 = new ConfirmWindow("再次确认", "该操作会删除该老师及其所有学员排课，并清除相关临时调整。\n\n确定继续？", "继续删除", "取消");
            var ok2 = await confirm2.ShowDialog<bool>(this);
            if (!ok2)
                return;
        }

        App.Services.Overrides.DeleteByTeacherId(vm.SelectedTeacher.Id);
        if (courseCount == 0)
        {
            App.Services.Teachers.DeleteById(vm.SelectedTeacher.Id);
        }
        else
        {
            App.Services.Teachers.DeleteCascade(vm.SelectedTeacher.Id);
        }

        vm.ReloadTeachers();
        vm.ReloadWeekSchedule();
    }


    private async void OpenTeacherScheduleButtonOnClick(object? sender, RoutedEventArgs e)
    {
        await OpenTeacherScheduleAsync();
    }

    private Task OpenTeacherScheduleAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
            return Task.CompletedTask;
        if (vm.SelectedTeacher is null)
            return Task.CompletedTask;

        var w = new TeacherScheduleWindow
        {
            DataContext = new TeacherScheduleWindowViewModel(App.Services, vm.SelectedTeacher)
        };
        w.Show(this);
        return Task.CompletedTask;
    }

    private async void AddCourseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var w = new CourseEditorWindow(App.Services, null, vm.SelectedTeacher);
        await w.ShowDialog(this);
        if (w.ResultCourse is not null)
        {
            vm.ReloadTeachers();
            vm.GoToWeek(GetFirstOccurrenceDate(w.ResultCourse));
        }
    }

    private async void EditCourseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var pick = new SelectCourseWindow
        {
            DataContext = new SelectCourseDialogViewModel(App.Services, vm.SelectedTeacher)
        };
        pick.Title = "选择要删除的学员";
        await pick.ShowDialog(this);
        if (pick.ResultCourseId is null)
            return;

        var course = App.Services.Courses.GetById(pick.ResultCourseId);
        if (course is null)
            return;

        var editor = new CourseEditorWindow(App.Services, course, vm.SelectedTeacher);
        await editor.ShowDialog(this);
        if (editor.ResultCourse is not null)
        {
            vm.ReloadTeachers();
            vm.GoToWeek(GetFirstOccurrenceDate(editor.ResultCourse));
        }
    }

    private async void DeleteCourseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var pick = new SelectCourseWindow
        {
            DataContext = new SelectCourseDialogViewModel(App.Services, vm.SelectedTeacher)
        };
        await pick.ShowDialog(this);
        if (pick.ResultCourseId is null)
            return;

        var course = App.Services.Courses.GetById(pick.ResultCourseId);
        if (course is null)
            return;

        var message =
            $"确认删除学员排课？\n\n{course.StudentName}\n{course.Content}\n{WeekdayUtil.ToChinese(course.Weekday)} {TimeUtil.FormatMinutes(course.StartMinute)}-{TimeUtil.FormatMinutes(course.EndMinute)}\n\n删除后不可恢复。";
        var confirm = new ConfirmWindow("删除学员确认", message, "删除", "取消");
        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok)
            return;

        App.Services.Overrides.DeleteByCourseId(course.Id);
        App.Services.Courses.DeleteById(course.Id);
        vm.ReloadTeachers();
        vm.ReloadWeekSchedule();
    }

    private static DateOnly GetFirstOccurrenceDate(Course course)
    {
        var start = course.StartDate;
        var startWeekday = ToWeekday1To7(start);
        var delta = (course.Weekday - startWeekday + 7) % 7;
        return start.AddDays(delta);
    }

    private static int ToWeekday1To7(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        return dow == 0 ? 7 : dow;
    }
}
