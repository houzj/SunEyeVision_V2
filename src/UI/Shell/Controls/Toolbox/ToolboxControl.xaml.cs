using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;
public partial class ToolboxControl : UserControl
{
    public static readonly DependencyProperty PointerLeftProperty = DependencyProperty.RegisterAttached("PointerLeft", typeof(bool), typeof(ToolboxControl), new PropertyMetadata(true));
    public static void SetPointerLeft(DependencyObject target, bool value)=>target.SetValue(PointerLeftProperty,value);
    public static bool GetPointerLeft(DependencyObject target)=>(bool)target.GetValue(PointerLeftProperty);
    private readonly HashSet<FrameworkElement> _categories = new();
    private ToolboxViewModel? Model=>DataContext as ToolboxViewModel;
    private Border? _armed;
    private ToolItem? _tool;
    private bool _dragging;
    private Window? _window;
    public ToolboxControl()
    {
        InitializeComponent(); Loaded+=OnLoaded; Unloaded+=OnUnloaded;
        DataContextChanged+=(_,e)=>{if(e.OldValue is ToolboxViewModel old){old.PropertyChanged-=ModelChanged;old.ClosePopup();}ResetCategories();if(e.NewValue is ToolboxViewModel next)next.PropertyChanged+=ModelChanged;};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
    }
    private void OnLoaded(object sender,RoutedEventArgs e)
    {
        if(Model is { } model){model.PropertyChanged-=ModelChanged;model.PropertyChanged+=ModelChanged;}
        _window=Window.GetWindow(this);
        if(_window!=null) { _window.Deactivated-=WindowDeactivated; _window.Deactivated+=WindowDeactivated; }
    }
    private void OnUnloaded(object sender,RoutedEventArgs e)
    {
        Close();ResetCategories();_armed=null;_tool=null;
        if(Model is { } model)model.PropertyChanged-=ModelChanged;
        if(_window!=null)_window.Deactivated-=WindowDeactivated;
        _window=null;
    }
    private void WindowDeactivated(object? sender,EventArgs e){if(!_dragging)Close();}
    private void ResetCategories(){foreach(var item in _categories)SetPointerLeft(item,true);_categories.Clear();}
    private void ModelChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(ToolboxViewModel.IsCompactMode)&&sender is ToolboxViewModel {IsCompactMode:false})ResetCategories();}
    private void Close(){ResetCategories();Model?.ClosePopup();}
    private void CategoryEnter(object sender,MouseEventArgs e)=>Open(sender);
    private void CategoryFocus(object sender,KeyboardFocusChangedEventArgs e)=>Open(sender);
    private void Open(object sender)
    {
        if(_dragging||Model is not {IsCompactMode:true} model||sender is not FrameworkElement {Tag:ToolCategory category})return;
        foreach(var item in _categories)if(!ReferenceEquals(item,sender))SetPointerLeft(item,true);
        _categories.Add((FrameworkElement)sender);
        SetPointerLeft((FrameworkElement)sender,false);
        model.SelectedCategory=category.Name;
    }
    private void CategoryLeave(object sender,MouseEventArgs e)
    {
        if(sender is not FrameworkElement item)return;
        var p=e.GetPosition(item);
        if(p.X<0||p.Y<0||p.X>item.RenderSize.Width||p.Y>item.RenderSize.Height)SetPointerLeft(item,true);
    }
    // 原件以工具箱根控件 IsMouseOver 控制整个分类弹窗会话。
    private void ToolboxLeave(object sender,MouseEventArgs e){if(!_dragging&&!IsMouseOver)Close();}
    private void ToolDown(object sender,MouseButtonEventArgs e)
    { if(sender is Border {Tag:ToolItem {IsAvailable:true} tool} border && tool.CanActivate()){_armed=border;_tool=tool;} }
    private void ToolUp(object sender,MouseButtonEventArgs e){_armed=null;_tool=null;}
    private void ToolLostCapture(object sender,MouseEventArgs e){if(!_dragging){_armed=null;_tool=null;}}
    private void ToolMove(object sender,MouseEventArgs e)
    {
        if(_dragging||_armed==null||_tool==null)return;
        if(!ReferenceEquals(sender,_armed)){_armed=null;_tool=null;return;}
        var source=_armed;var tool=_tool;_armed=null;_tool=null;
        if(!tool.CanActivate()||e.LeftButton!=MouseButtonState.Pressed)return;
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
