using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class TeacherColorWindow : Window
{
    private readonly AppServices _services;

    public TeacherColorWindow(AppServices services, TeacherColorDialogViewModel viewModel)
    {
        _services = services;
        InitializeComponent();
        DataContext = viewModel;

        OkButton.Click += OkButtonOnClick;
        CancelButton.Click += (_, _) => Close(false);
    }

    private void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TeacherColorDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        try
        {
            _services.Teachers.SetColorHex(vm.Teacher.Id, vm.SelectedColor?.Hex);
            Close(true);
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}

