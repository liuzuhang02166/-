using System;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class ExportPreviewWindow : Window
{
    private readonly AppServices _services;

    public ExportPreviewWindow(AppServices services, DateOnly anchorDate, Models.Teacher? defaultTeacher)
    {
        _services = services;
        InitializeComponent();
        DataContext = new ExportPreviewDialogViewModel(services, anchorDate, defaultTeacher);

        CancelButton.Click += (_, _) => Close();
        ExportButton.Click += ExportButtonOnClick;

        if (DataContext is ExportPreviewDialogViewModel initVm)
        {
            ThisWeekRadio.IsChecked = initVm.DateMode == ExportDateMode.ThisWeek;
            NextWeekRadio.IsChecked = initVm.DateMode == ExportDateMode.NextWeek;
            CustomRangeRadio.IsChecked = initVm.DateMode == ExportDateMode.Custom;
        }

        ThisWeekRadio.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is ExportPreviewDialogViewModel vm && ThisWeekRadio.IsChecked == true)
                vm.DateMode = ExportDateMode.ThisWeek;
        };
        NextWeekRadio.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is ExportPreviewDialogViewModel vm && NextWeekRadio.IsChecked == true)
                vm.DateMode = ExportDateMode.NextWeek;
        };
        CustomRangeRadio.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is ExportPreviewDialogViewModel vm && CustomRangeRadio.IsChecked == true)
                vm.DateMode = ExportDateMode.Custom;
        };

        AllTeachersRadio.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is ExportPreviewDialogViewModel vm && AllTeachersRadio.IsChecked == true)
                vm.ExportAllTeachers = true;
        };
        SingleTeacherRadio.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is ExportPreviewDialogViewModel vm && SingleTeacherRadio.IsChecked == true)
                vm.ExportAllTeachers = false;
        };
    }

    private async void ExportButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ExportPreviewDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        try
        {
            var teacherId = vm.GetTeacherIdFilter();
            var teacherName = teacherId is null ? "全部老师" : (vm.SelectedTeacher?.Name ?? "老师");
            if (!vm.TryGetSelectedRange(out var start, out var end, out var msg))
                throw new InvalidOperationException(msg);
            var fileName = $"{teacherName}_排课表_{start:yyyyMMdd}-{end:yyyyMMdd}.pdf";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存 PDF",
                SuggestedFileName = fileName,
                DefaultExtension = "pdf",
                FileTypeChoices =
                [
                    new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }
                ]
            });

            var path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            var exporter = new PdfExportService(_services);
            exporter.ExportRange(path, start, end, teacherId, vm.IncludeWeekNotes);
            Close();
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}
