using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace SunEyeVision.UI.Shell.Windows;
public partial class WorkflowIdSearchWindow : ThemeDialogWindow
{
 internal Func<string,string?>? Navigate {get;set;}
 public WorkflowIdSearchWindow(){InitializeComponent();Loaded+=(_,_)=>Query.Focus();Closed+=(_,_)=>Navigate=null;}
 private void QueryChanged(object sender,TextChangedEventArgs e){if(Error!=null)Error.Text=string.Empty;}
 private void WindowKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Close();}}
 private void SearchClick(object sender,RoutedEventArgs e)
 {
  var query=Query.Text.Trim();
  if(query.Length==0){Error.Text="请输入节点序号或稳定ID。";Query.Focus();return;}
  var error=Navigate==null?"搜索服务尚未就绪。":Navigate(query);
  if(error==null){if(IsVisible)Close();return;}
  Error.Text=error;Query.Focus();Query.SelectAll();
 }
}
