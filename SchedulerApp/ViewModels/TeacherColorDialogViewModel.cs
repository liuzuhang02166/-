using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SchedulerApp.Models;

namespace SchedulerApp.ViewModels;

public partial class TeacherColorDialogViewModel : ViewModelBase
{
    public Teacher Teacher { get; }

    public ObservableCollection<TeacherColorOption> ColorOptions { get; } = new(TeacherColorOptions.All);

    [ObservableProperty]
    private TeacherColorOption? selectedColor;

    [ObservableProperty]
    private string error = string.Empty;

    public TeacherColorDialogViewModel(Teacher teacher)
    {
        Teacher = teacher;
        SelectedColor = ColorOptions.FirstOrDefault(x => string.Equals(x.Hex, teacher.ColorHex, StringComparison.OrdinalIgnoreCase))
                        ?? ColorOptions[0];
    }
}
