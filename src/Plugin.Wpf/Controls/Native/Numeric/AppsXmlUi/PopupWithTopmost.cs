using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
/// <summary>Original placement and Z-order behavior; subscriptions belong to the open popup.</summary>
public class PopupWithTopmost : Popup
{
    public static readonly DependencyProperty TopmostProperty = Window.TopmostProperty.AddOwner(typeof(PopupWithTopmost),new FrameworkPropertyMetadata(false,(d,_)=>((PopupWithTopmost)d).UpdateWindow()));
    public static readonly DependencyProperty PopupPlacementTargetProperty=DependencyProperty.RegisterAttached("PopupPlacementTarget",typeof(DependencyObject),typeof(PopupWithTopmost),new PropertyMetadata(null,(d,_)=>((PopupWithTopmost)d).AttachTarget()));
    private FrameworkElement? _target;
    private Window? _owner;
    public bool Topmost {get=>(bool)GetValue(TopmostProperty);set=>SetCurrentValue(TopmostProperty,value);}
    public DependencyObject? PopupPlacementTarget {get=>(DependencyObject?)GetValue(PopupPlacementTargetProperty);set=>SetCurrentValue(PopupPlacementTargetProperty,value);}
    public new event EventHandler? Opened;
    public PopupWithTopmost()=>Unloaded+=(_,_)=>CloseAndDetach();
    private void DetachTarget()
    {
        DetachOwner();
        if(_target is not null){_target.Loaded-=TargetLoaded;_target.Unloaded-=TargetUnloaded;}
        _target=null;
    }
    private void DetachOwner()
    {
        if(_owner is not null){_owner.LocationChanged-=OwnerMoved;_owner.Closed-=OwnerClosed;}
        _owner=null;
    }
    private void AttachTarget()
    {
        DetachTarget();if(!IsOpen)return;
        _target=(PopupPlacementTarget??PlacementTarget) as FrameworkElement;
        if(_target is null)return;
        _target.Loaded+=TargetLoaded;_target.Unloaded+=TargetUnloaded;AttachOwner();
    }
    private void AttachOwner()
    {
        DetachOwner();if(!IsOpen||_target?.IsLoaded!=true)return;
        _owner=Window.GetWindow(_target);
        if(_owner is not null){_owner.LocationChanged+=OwnerMoved;_owner.Closed+=OwnerClosed;}
    }
    private void TargetLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(sender,_target))AttachOwner();}
    private void TargetUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(sender,_target))CloseAndDetach();}
    private void OwnerClosed(object? sender,EventArgs args){if(ReferenceEquals(sender,_owner))CloseAndDetach();}
    private void OwnerMoved(object? sender,EventArgs args)
    {
        if(!IsOpen||!ReferenceEquals(sender,_owner))return;
        var owner=_owner;var offset=HorizontalOffset;
        SetCurrentValue(HorizontalOffsetProperty,offset+1);
        if(IsOpen&&ReferenceEquals(owner,_owner))SetCurrentValue(HorizontalOffsetProperty,offset);
    }
    private void CloseAndDetach(){try{if(IsOpen)SetCurrentValue(IsOpenProperty,false);}finally{DetachTarget();}}
    protected override void OnOpened(EventArgs args)
    {
        // Preserve the original derived event without delivering it twice through base.
        AttachTarget();try{Opened?.Invoke(this,args);}finally{UpdateWindow();}
    }
    protected override void OnClosed(EventArgs args){DetachTarget();base.OnClosed(args);}
    private void UpdateWindow()
    {
        if(!IsOpen||Child is null||PresentationSource.FromVisual(Child) is not HwndSource source)return;
        // Auto dimensions may be NaN: change only Z-order, retaining the original layout.
        SetWindowPos(source.Handle,new IntPtr(Topmost?-1:-2),0,0,0,0,0x0001|0x0002|0x0010);
    }
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
