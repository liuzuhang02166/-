using System;
using Avalonia.Controls;
using SchedulerApp.Models;
using SchedulerApp.Services;
using SchedulerApp.ViewModels;

namespace SchedulerApp.Views;

public partial class OverrideListWindow : Window
{
    private readonly AppServices _services;
    private readonly Teacher? _defaultTeacher;
    private bool _changed;

    public OverrideListWindow(AppServices services, Teacher? defaultTeacher)
    {
        _services = services;
        _defaultTeacher = defaultTeacher;

        InitializeComponent();
        DataContext = new OverrideListWindowViewModel(services);

        RefreshButton.Click += (_, _) => (DataContext as OverrideListWindowViewModel)?.Reload();
        CloseButton.Click += (_, _) => Close(_changed);

        AddButton.Click += AddButtonOnClick;
        EditButton.Click += EditButtonOnClick;
        DeleteButton.Click += DeleteButtonOnClick;
    }

    private async void AddButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var w = new OverrideEditorWindow(_services, _defaultTeacher);
        var ok = await w.ShowDialog<bool>(this);
        if (!ok)
            return;

        _changed = true;
        (DataContext as OverrideListWindowViewModel)?.Reload();
    }

    private async void EditButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not OverrideListWindowViewModel vm)
            return;
        if (vm.SelectedItem is null)
            return;

        var w = new OverrideEditorWindow(_services, _defaultTeacher, vm.SelectedItem.Entry);
        var ok = await w.ShowDialog<bool>(this);
        if (!ok)
            return;

        _changed = true;
        vm.Reload();
    }

    private async void DeleteButtonOnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not OverrideListWindowViewModel vm)
            return;
        if (vm.SelectedItem is null)
            return;

        var item = vm.SelectedItem;
        var msg =
            $"确认删除该临时调整？\n\n{item.DateText} {item.KindText}\n{item.TeacherText}\n{item.TimeText} {item.StudentText} {item.ContentText}\n\n删除后不可恢复。";
        var confirm = new ConfirmWindow("删除调整确认", msg, "删除", "取消");
        var ok = await confirm.ShowDialog<bool>(this);
        if (!ok)
            return;

        _services.Overrides.DeleteById(item.Entry.Id);
        _changed = true;
        vm.Reload();
    }
}

