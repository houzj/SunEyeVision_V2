using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationRenameDialog : ThemeDialogWindow
{
    public CommunicationRenameDialog(string currentName, string entityLabel = "设备")
    {
        InitializeComponent();
        Title = $"重命名{entityLabel}";
        NameLabel.Text = $"{entityLabel}名称";
        NameTextBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public string DeviceName => NameTextBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ValidationText.Text = "设备名称不能为空。";
            NameTextBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm_Click(sender, e);
        }
    }
}
