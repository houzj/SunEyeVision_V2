using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Themes;

namespace SunEyeVision.Tool.NPointCalibration.Views;

/// <summary>Plugin-local themed modal shell. Does not schedule or access devices.</summary>
internal static class CalibrationDialogs
{
    private static Window Shell(FrameworkElement owner,string title,double width=510,double height=240)
    {
        var window=new Window {Title=title,Width=width,Height=height,MinWidth=440,MinHeight=180,
            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.CanResizeWithGrip,ShowInTaskbar=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Window.GetWindow(owner)};
        window.SetResourceReference(Control.BackgroundProperty,SdkThemeKeys.PanelBackgroundBrush);
        window.SetResourceReference(Control.ForegroundProperty,SdkThemeKeys.TextPrimaryBrush);
        return window;
    }
    private static DockPanel Layout(Window w,string title,out StackPanel buttons)
    {
        var root=new DockPanel {Margin=new Thickness(14)};
        var header=new DockPanel {Height=34};DockPanel.SetDock(header,Dock.Top);
        var close=new Button {Content="×",Width=30};close.Click+=(_,_)=>w.Close();DockPanel.SetDock(close,Dock.Right);
        header.Children.Add(close);header.Children.Add(new TextBlock {Text=title,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)w.DragMove();};root.Children.Add(header);
        buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);w.Content=root;return root;
    }
    public static bool Confirm(FrameworkElement owner,string title,string text)
    {
        var w=Shell(owner,title);var root=Layout(w,title,out var buttons);
        root.Children.Add(new TextBlock {Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)});
        var cancel=new Button {Content="取消",MinWidth=72,Margin=new Thickness(4)};cancel.Click+=(_,_)=>w.DialogResult=false;
        var ok=new Button {Content="确定",MinWidth=72,Margin=new Thickness(4)};ok.Click+=(_,_)=>w.DialogResult=true;
        buttons.Children.Add(cancel);buttons.Children.Add(ok);return w.ShowDialog()==true;
    }
    public static void Error(FrameworkElement owner,string title,string text)
    {
        var w=Shell(owner,title,560,280);var root=Layout(w,title,out var buttons);
        root.Children.Add(new TextBox {Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new Thickness(0),
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,12,0,0)});
        var ok=new Button {Content="确定",MinWidth=72};ok.Click+=(_,_)=>w.Close();buttons.Children.Add(ok);w.ShowDialog();
    }
    public static void Residuals(FrameworkElement owner,NPointCalibrationResults result,bool current,double threshold)
    {
        var w=Shell(owner,"标定逐点误差",860,500);var root=Layout(w,"标定逐点误差",out var buttons);
        var status=new TextBlock {Text=current?"当前结果有效":"结果已过期，不代表当前点表",Margin=new Thickness(0,8,0,8)};
        DockPanel.SetDock(status,Dock.Top);root.Children.Add(status);
        var grid=new DataGrid {IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,
            ItemsSource=result.Residuals.Select(r=>new {Point=r.DisplayId,Kind=r.KindText,r.ErrorX,r.ErrorY,r.ErrorMm,
                Judgment=current?(r.ErrorMm<=threshold?"合格":"超限"):"已过期"})};
        foreach(var column in new[]{("点号","Point"),("类别","Kind"),("误差X(mm)","ErrorX"),("误差Y(mm)","ErrorY"),("距离(mm)","ErrorMm"),("判断","Judgment")})
            grid.Columns.Add(new DataGridTextColumn {Header=column.Item1,Binding=new System.Windows.Data.Binding(column.Item2),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        root.Children.Add(grid);var close=new Button {Content="关闭",MinWidth=72};close.Click+=(_,_)=>w.Close();buttons.Children.Add(close);w.ShowDialog();
    }
}
