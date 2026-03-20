using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class AddTeacherWindow : Window
{
    private readonly AppServices _services;
    public Teacher? ResultTeacher { get; private set; }

    public AddTeacherWindow(AppServices services)
    {
        _services = services;
        InitializeComponent();
        DataContext = new AddTeacherDialogViewModel();

        OkButton.Click += OkButtonOnClick;
        CancelButton.Click += (_, _) => Close();
    }

    private void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddTeacherDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        try
        {
            ResultTeacher = _services.Teachers.Add(vm.Name, vm.SelectedColor?.Hex);
            Close(ResultTeacher);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}
