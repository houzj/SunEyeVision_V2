using System.Collections.Generic;
using SunEyeVision.UI.Shell.Windows;
namespace SunEyeVision.UI.Features.Dialogs;

public partial class SolutionInformationDialog : ThemeDialogWindow
{
    public SolutionInformationDialog(string title, IEnumerable<KeyValuePair<string, string>> fields)
    {
        InitializeComponent();
        Title = title; InformationTitle.Text = title; InformationFields.ItemsSource = fields;
    }
}
