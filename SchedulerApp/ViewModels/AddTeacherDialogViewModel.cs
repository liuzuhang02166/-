using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulerApp.ViewModels;

public partial class AddTeacherDialogViewModel : ViewModelBase
{
    public ObservableCollection<TeacherColorOption> ColorOptions { get; } = new(TeacherColorOptions.All);

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private TeacherColorOption? selectedColor;

    [ObservableProperty]
    private string error = string.Empty;

    public AddTeacherDialogViewModel()
    {
        SelectedColor = ColorOptions[0];
    }
}
