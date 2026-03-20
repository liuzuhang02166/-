using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchedulerApp.Domain;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class OverrideEditorWindow : Window
{
    private readonly AppServices _services;
    public bool Saved { get; private set; }

    public OverrideEditorWindow(AppServices services, Teacher? defaultTeacher)
    {
        _services = services;
        InitializeComponent();
        DataContext = new OverrideEditorDialogViewModel(services, defaultTeacher, null);
        CancelButton.Click += (_, _) => Close(false);
        OkButton.Click += OkButtonOnClick;
    }

    public OverrideEditorWindow(AppServices services, Teacher? defaultTeacher, OverrideEntry editingEntry)
    {
        _services = services;
        InitializeComponent();
        DataContext = new OverrideEditorDialogViewModel(services, defaultTeacher, editingEntry);
        CancelButton.Click += (_, _) => Close(false);
        OkButton.Click += OkButtonOnClick;
    }

    private async void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OverrideEditorDialogViewModel vm)
            return;

        vm.Error = string.Empty;
        try
        {
            var date = vm.GetSelectedDateOnly() ?? throw new InvalidOperationException("请选择日期。");
            var kind = vm.SelectedAction.Kind;
            var now = DateTimeOffset.UtcNow;
            var editingId = vm.EditingOverrideId;
            var createdAt = vm.EditingCreatedAt ?? now;

            if (kind == OverrideKind.Cancel)
            {
                if (vm.SelectedTeacher is null)
                    throw new InvalidOperationException("请选择老师。");
                if (vm.SelectedCourse is null)
                    throw new InvalidOperationException("请选择要停课的课程。");

                var entry = new OverrideEntry(
                    editingId ?? Guid.NewGuid().ToString("N"),
                    OverrideKind.Cancel,
                    date,
                    vm.SelectedCourse.CourseId,
                    vm.SelectedTeacher.Id,
                    null,
                    null,
                    null,
                    null,
                    null,
                    vm.Note ?? string.Empty,
                    false,
                    createdAt,
                    now
                );
                if (editingId is null)
                    _services.Overrides.Add(entry);
                else
                    _services.Overrides.Update(entry);
                Saved = true;
                Close(true);
                return;
            }

            if (kind == OverrideKind.Move)
            {
                if (vm.SelectedTeacher is null)
                    throw new InvalidOperationException("请选择老师。");
                if (vm.SelectedCourse is null)
                    throw new InvalidOperationException("请选择要窜课的课程。");
                if (vm.TargetTeacher is null)
                    throw new InvalidOperationException("请选择目标老师。");

                var course = _services.Courses.GetById(vm.SelectedCourse.CourseId);
                if (course is null)
                    throw new InvalidOperationException("原课程不存在。");

                var startMinute = TimeUtil.ParseHhMm(vm.StartTime);
                var endMinute = TimeUtil.ParseHhMm(vm.EndTime);
                if (endMinute <= startMinute)
                    throw new InvalidOperationException("结束时间必须晚于开始时间。");

                var wouldConflict = vm.WouldConflict(vm.TargetTeacher.Id, date, startMinute, endMinute, course.Id);
                var isForced = false;
                if (wouldConflict)
                {
                    var msg =
                        $"目标老师：{vm.TargetTeacher.Name}\n日期：{date:yyyy-MM-dd}\n时间：{vm.StartTime}-{vm.EndTime}\n\n是否强制覆盖并保存本次临时调整？";
                    var confirm = new ConflictConfirmWindow { DataContext = msg };
                    var ok = await confirm.ShowDialog<bool>(this);
                    if (!ok)
                        return;
                    isForced = confirm.ForceOverride;
                }

                var entry = new OverrideEntry(
                    editingId ?? Guid.NewGuid().ToString("N"),
                    OverrideKind.Move,
                    date,
                    course.Id,
                    vm.SelectedTeacher.Id,
                    vm.TargetTeacher.Id,
                    course.StudentName,
                    course.Content,
                    startMinute,
                    endMinute,
                    vm.Note ?? string.Empty,
                    isForced,
                    createdAt,
                    now
                );
                if (editingId is null)
                    _services.Overrides.Add(entry);
                else
                    _services.Overrides.Update(entry);
                Saved = true;
                Close(true);
                return;
            }

            if (kind == OverrideKind.Add)
            {
                if (vm.TargetTeacher is null)
                    throw new InvalidOperationException("请选择目标老师。");
                if (string.IsNullOrWhiteSpace(vm.StudentName))
                    throw new InvalidOperationException("学员名称不能为空。");
                if (string.IsNullOrWhiteSpace(vm.Content))
                    throw new InvalidOperationException("教学内容不能为空。");

                var startMinute = TimeUtil.ParseHhMm(vm.StartTime);
                var endMinute = TimeUtil.ParseHhMm(vm.EndTime);
                if (endMinute <= startMinute)
                    throw new InvalidOperationException("结束时间必须晚于开始时间。");

                var wouldConflict = vm.WouldConflict(vm.TargetTeacher.Id, date, startMinute, endMinute, null);
                var isForced = false;
                if (wouldConflict)
                {
                    var msg =
                        $"目标老师：{vm.TargetTeacher.Name}\n日期：{date:yyyy-MM-dd}\n时间：{vm.StartTime}-{vm.EndTime}\n\n是否强制覆盖并保存本次加课？";
                    var confirm = new ConflictConfirmWindow { DataContext = msg };
                    var ok = await confirm.ShowDialog<bool>(this);
                    if (!ok)
                        return;
                    isForced = confirm.ForceOverride;
                }

                var entry = new OverrideEntry(
                    editingId ?? Guid.NewGuid().ToString("N"),
                    OverrideKind.Add,
                    date,
                    null,
                    null,
                    vm.TargetTeacher.Id,
                    vm.StudentName.Trim(),
                    vm.Content.Trim(),
                    startMinute,
                    endMinute,
                    vm.Note ?? string.Empty,
                    isForced,
                    createdAt,
                    now
                );
                if (editingId is null)
                    _services.Overrides.Add(entry);
                else
                    _services.Overrides.Update(entry);
                Saved = true;
                Close(true);
            }
        }
        catch (Exception ex)
        {
            vm.Error = ex.Message;
        }
    }
}
