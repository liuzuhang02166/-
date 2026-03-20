using Avalonia.Controls;

namespace SchedulerApp.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string message, string okText, string cancelText)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        CancelButton.Content = cancelText;

        OkButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
    }
}

