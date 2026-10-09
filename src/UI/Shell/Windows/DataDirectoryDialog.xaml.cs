using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;

public partial class DataDirectoryDialog : ThemeDialogWindow
{
    private readonly ApplicationDataLocationService _locations;
    public ApplicationDataPaths? SelectedPaths { get; private set; }
    public bool ImportLegacy => ImportBox.IsChecked == true;
    public DataDirectoryDialog(ApplicationDataLocationService locations, string? initial = null, string? reason = null, bool allowLegacy = true)
    {
        InitializeComponent();
        _locations = locations;
        var system = Path.GetPathRoot(Environment.SystemDirectory);
        var preferred = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed &&
            !string.Equals(d.RootDirectory.FullName, system, StringComparison.OrdinalIgnoreCase))?.RootDirectory.FullName;
        PathBox.Text = initial ?? Path.Combine(preferred ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SunEyeVisionData");
        ImportBox.Visibility = allowLegacy && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "solution_settings.json")) ? Visibility.Visible : Visibility.Collapsed;
        ImportBox.IsChecked = ImportBox.Visibility == Visibility.Visible;
        ErrorText.Text = reason ?? "";
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 SunEyeVision 数据目录" };
        if (Directory.Exists(PathBox.Text)) dialog.InitialDirectory = PathBox.Text;
        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FolderName;
    }
    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectedPaths = _locations.Prepare(PathBox.Text.Trim());
            DialogResult = true;
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; }
    }
}
