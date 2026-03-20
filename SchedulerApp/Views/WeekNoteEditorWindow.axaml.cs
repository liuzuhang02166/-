using System;
using Avalonia.Controls;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class WeekNoteEditorWindow : Window
{
    public WeekNoteEditorWindow(AppServices services, DateOnly anchorDate)
    {
        InitializeComponent();
        DataContext = new WeekNoteEditorDialogViewModel(services, anchorDate);
        CancelButton.Click += (_, _) => Close(false);
        OkButton.Click += (_, _) =>
        {
            if (DataContext is WeekNoteEditorDialogViewModel vm)
            {
                vm.Save();
                Close(true);
            }
        };
    }
}

