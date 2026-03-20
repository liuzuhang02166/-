using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class SelectCourseWindow : Window
{
    public string? ResultCourseId { get; private set; }

    public SelectCourseWindow()
    {
        InitializeComponent();
        OkButton.Click += OkButtonOnClick;
        CancelButton.Click += (_, _) => Close();
    }

    private void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SelectCourseDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        if (vm.SelectedCourse is null)
        {
            vm.Error = "请选择一个学员。";
            return;
        }

        ResultCourseId = vm.SelectedCourse.CourseId;
        Close(ResultCourseId);
    }
}

