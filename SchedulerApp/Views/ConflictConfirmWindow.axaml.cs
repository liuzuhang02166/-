using Avalonia.Controls;

namespace SchedulerApp.Views;

public partial class ConflictConfirmWindow : Window
{
    public bool ForceOverride { get; private set; }

    public ConflictConfirmWindow()
    {
        InitializeComponent();
        BackButton.Click += (_, _) =>
        {
            ForceOverride = false;
            Close(false);
        };
        ForceButton.Click += (_, _) =>
        {
            ForceOverride = true;
            Close(true);
        };
    }
}

