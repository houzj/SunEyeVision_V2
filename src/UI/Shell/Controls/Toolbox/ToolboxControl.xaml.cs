using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;
public partial class ToolboxControl : UserControl
{
    private ToolboxViewModel? Model=>DataContext as ToolboxViewModel;
    private FrameworkElement? _anchor;
    private Border? _armed;
    private ToolItem? _tool;
    private bool _dragging;
    private Window? _window;
    private long _generation;
    public ToolboxControl()
    {
        InitializeComponent(); Loaded+=OnLoaded; Unloaded+=OnUnloaded;
        DataContextChanged+=(_,e)=>{Close();ToolPopup.DataContext=e.NewValue;};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
    }
    private void OnLoaded(object sender,RoutedEventArgs e)
    {
        ToolPopup.DataContext=DataContext;
        _window=Window.GetWindow(this);
        if(_window!=null) { _window.Deactivated-=WindowDeactivated; _window.Deactivated+=WindowDeactivated; }
    }
    private void OnUnloaded(object sender,RoutedEventArgs e)
    {
        Close();_armed=null;_tool=null;
        if(_window!=null)_window.Deactivated-=WindowDeactivated;
        _window=null;
    }
    private void WindowDeactivated(object? sender,EventArgs e){if(!_dragging)Close();}
    private void Close(){_generation++;Model?.ClosePopup();_anchor=null;}
    private void CategoryEnter(object sender,MouseEventArgs e)=>Open(sender);
    private void CategoryFocus(object sender,KeyboardFocusChangedEventArgs e)=>Open(sender);
    private void Open(object sender)
    {
        if(_dragging||Model is not {IsCompactMode:true} model||sender is not FrameworkElement {Tag:ToolCategory category} anchor)return;
        _generation++;_anchor=anchor;ToolPopup.PlacementTarget=anchor;
        ToolPopup.MaxHeight=Math.Max(0,ActualHeight);
        PopupScroll.MaxHeight=Math.Max(0,ActualHeight-80);
        model.SelectedCategory=category.Name;
        PopupHitArea.DataContext=model;
        PopupTitle.Text=category.Name;
        model.IsCompactModePopupOpen=true;
    }
    private void CategoryLeave(object sender,MouseEventArgs e)=>ScheduleClose();
    private void PopupEnter(object sender,MouseEventArgs e){_generation++;}
    private void PopupLeave(object sender,MouseEventArgs e)=>ScheduleClose();
    private void ScheduleClose()
    {
        var generation=++_generation;
        Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(()=>
        {
            if(generation!=_generation||_dragging||_anchor?.IsMouseOver==true||PopupHitArea.IsMouseOver)return;
            // Popup独立呈现源：局部坐标直接比较DIP，不把屏幕像素加上DIP宽高。
            if(Inside(_anchor)||Inside(PopupHitArea))return;
            Close();
        }));
    }
    private static bool Inside(FrameworkElement? element)
    {
        if(element?.IsVisible!=true)return false;
        var p=Mouse.GetPosition(element);
        return p.X>=0&&p.Y>=0&&p.X<=element.ActualWidth&&p.Y<=element.ActualHeight;
    }
    private void ToolDown(object sender,MouseButtonEventArgs e)
    { if(sender is Border {Tag:ToolItem {IsAvailable:true} tool} border){_armed=border;_tool=tool;} }
    private void ToolUp(object sender,MouseButtonEventArgs e){_armed=null;_tool=null;}
    private void ToolLostCapture(object sender,MouseEventArgs e){if(!_dragging){_armed=null;_tool=null;}}
    private void ToolMove(object sender,MouseEventArgs e)
    {
        if(_dragging||_armed==null||_tool==null)return;
        if(!ReferenceEquals(sender,_armed)){_armed=null;_tool=null;return;}
        var source=_armed;var tool=_tool;_armed=null;_tool=null;
        if(!tool.IsAvailable||e.LeftButton!=MouseButtonState.Pressed)return;
        _dragging=true;
        try
        {
            Close();
            // 保留现行画布的ToolItem新增事务；程序集创建失败由该事务回滚和诊断。
            DragDrop.DoDragDrop(source,new DataObject("ToolItem",tool),DragDropEffects.Copy);
        }
        catch(Exception ex){VisionLogger.Instance.Log(LogLevel.Error,"工具拖动失败："+tool.Name,"Toolbox",ex);}
        finally{_dragging=false;_armed=null;_tool=null;Close();}
    }
}