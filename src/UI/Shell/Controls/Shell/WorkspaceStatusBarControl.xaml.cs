using System;
using System.Windows;
using System.Windows.Controls;
namespace SunEyeVision.UI.Shell.Controls.Shell;
/// <summary>流程工作区状态与缩放入口；实际视口变换由宿主执行。</summary>
public partial class WorkspaceStatusBarControl : UserControl
{
 public static readonly DependencyProperty ScaleProperty=DependencyProperty.Register(nameof(Scale),typeof(double),typeof(WorkspaceStatusBarControl),new PropertyMetadata(1d));
 public static readonly DependencyProperty StatusTextProperty=DependencyProperty.Register(nameof(StatusText),typeof(string),typeof(WorkspaceStatusBarControl),new PropertyMetadata("等待添加模块"));
 public double Scale {get=>(double)GetValue(ScaleProperty);set=>SetValue(ScaleProperty,value);}
 public string StatusText {get=>(string)GetValue(StatusTextProperty);set=>SetValue(StatusTextProperty,value);}
 public event Action<double>? ScaleRequested;
 public WorkspaceStatusBarControl()=>InitializeComponent();
 private void ZoomSlider_ValueChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
 {
  if(double.IsFinite(e.NewValue)&&Math.Abs(e.NewValue-Scale)>0.00001)ScaleRequested?.Invoke(Math.Clamp(Math.Round(e.NewValue,1),0.1,2));
 }
 private void ResetZoomButton_Click(object sender,RoutedEventArgs e)=>ScaleRequested?.Invoke(1);
}
