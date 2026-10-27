using System.Globalization;
using System.Windows;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class RootWorkflowRunSettingsWindow:ThemeDialogWindow
{
 private readonly RootWorkspaceSettings _opening;
 public RootWorkspaceSettings Settings {get;private set;}
 public RootWorkflowRunSettingsWindow(RootWorkspaceSettings settings)
 {
  _opening=Settings=settings;InitializeComponent();Interval.Text=settings.RunIntervalMs.ToString(CultureInfo.InvariantCulture);StopOnNg.IsChecked=settings.StopOnNg;
  Loaded+=(_,_)=>{Interval.Focus();Interval.SelectAll();};
 }
 private void Apply(object sender,RoutedEventArgs e)
 {
  if(!int.TryParse(Interval.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var value)||value is <0 or >100000){Error.Text="请输入0到100000毫秒的整数。";Interval.Focus();return;}
  Settings=_opening with{RunIntervalMs=value,StopOnNg=StopOnNg.IsChecked==true};DialogResult=true;
 }
}
