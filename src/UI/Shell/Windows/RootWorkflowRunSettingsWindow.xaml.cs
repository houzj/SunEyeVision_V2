using System;
using System.Windows;
using SunEyeVision.Domain.Workflows;
namespace SunEyeVision.UI.Shell.Windows;
public partial class RootWorkflowRunSettingsWindow : ThemeDialogWindow
{
 private readonly RootWorkspaceSettings _opening;
 public RootWorkspaceSettings Settings { get; private set; }
 public RootWorkflowRunSettingsWindow(RootWorkspaceSettings settings)
 {
  _opening = Settings = settings ?? throw new ArgumentNullException(nameof(settings));
  InitializeComponent();
  Loaded += (_, _) => Interval.StartValue = settings.RunIntervalMs;
 }
 private void Apply(object sender, RoutedEventArgs e)
 {
  var value = Interval.StartValue;
  if (!double.IsFinite(value) || value < 0 || value > 100000 || value != Math.Truncate(value))
  { Error.Text = "请输入 0 到 100000 毫秒的整数。"; return; }
  Settings = _opening with { RunIntervalMs = (int)value };
  DialogResult = true;
 }
}
