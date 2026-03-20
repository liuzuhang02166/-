using System;
using Avalonia.Controls;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class TeacherScheduleWindow : Window
{
    public TeacherScheduleWindow()
    {
        InitializeComponent();

        EditTeacherColorButton.Click += EditTeacherColorButtonOnClick;
        PrevWeekButton.Click += (_, _) =>
        {
            if (DataContext is ViewModels.TeacherScheduleWindowViewModel vm)
                vm.GoPrevWeek();
        };
        NextWeekButton.Click += (_, _) =>
        {
            if (DataContext is ViewModels.TeacherScheduleWindowViewModel vm)
                vm.GoNextWeek();
        };
        ThisWeekButton.Click += (_, _) =>
        {
            if (DataContext is ViewModels.TeacherScheduleWindowViewModel vm)
                vm.GoToWeek(DateOnly.FromDateTime(DateTime.Today));
        };
    }

    private async void EditTeacherColorButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.TeacherScheduleWindowViewModel vm)
            return;

        var dlg = new TeacherColorWindow(App.Services, new ViewModels.TeacherColorDialogViewModel(vm.Teacher));
        var ok = await dlg.ShowDialog<bool>(this);
        if (!ok)
            return;

        vm.ReloadWeekSchedule();
        if (Owner?.DataContext is MainWindowViewModel main)
        {
            main.ReloadTeachers();
            main.ReloadWeekSchedule();
        }
    }
}
