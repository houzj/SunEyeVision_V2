using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.TestShared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shell.Controls.Shell;
using Xunit;

namespace SunEyeVision.ShellFooter.Tests;

[Collection("正式WPF owner")]
public sealed class FooterHostTests
{
 [Fact]
 public async Task Actual_log_summary_refreshes_and_unsubscribes_on_unload()
 {
  StatusBarControl? footer=null; Window? window=null;
  var unloaded=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  WpfTestDispatcher.Invoke(()=>{
   var writer=ServiceInitializer.UILogWriter;
   writer.Clear();
   footer=new StatusBarControl();
   footer.Unloaded+=(_,_)=>unloaded.TrySetResult();
   window=new Window{Content=footer,Width=700,Height=120,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};
   window.Show();
   writer.Write(LogEntry.Create(1,LogLevel.Warning,"真实测试日志"));
  });
  await Task.Delay(500);
  WpfTestDispatcher.Invoke(()=>{
   var summary=(Grid)footer!.FindName("LogSummary");
   Assert.Equal(Visibility.Visible,summary.Visibility);
   Assert.Equal("真实测试日志",Assert.IsType<LogEntry>(summary.DataContext).Message);
   var expand=FindVisualChildren<Button>(footer).Single();
   Assert.False(expand.IsEnabled);
   window!.Close();
  });
  await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));
  WpfTestDispatcher.Invoke(()=>{
   var summary=(Grid)footer!.FindName("LogSummary");
   Assert.Null(summary.DataContext);
   ServiceInitializer.UILogWriter.Write(LogEntry.Create(2,LogLevel.Error,"卸载后的日志"));
  });
  await Task.Delay(300);
  WpfTestDispatcher.Invoke(()=>Assert.Null(((Grid)footer!.FindName("LogSummary")).DataContext));
 }
 private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T:DependencyObject
 {
  for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
  {
   var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);
   if(child is T match)yield return match;
   foreach(var descendant in FindVisualChildren<T>(child))yield return descendant;
  }
 }
}
