using System.Windows;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

internal sealed partial class NativeRoiAttachment
{
    private RoiGeometryClipboard? _clipboard;
    private RoiEditStamp? _menuStamp;
    private NativeRoiSession? _menuOwner;
    private long _menuGeneration;
    private ContextMenu? _menuSurface;
    private RoutedEventHandler? _menuClosed;
    private bool _capturingMenu;
    private sealed record MenuLease(NativeRoiSession Owner,NativeResultFrameReceipt Frame,long Generation,RoiEditStamp Stamp);
    private bool Still(MenuLease lease,bool exactStamp=true)=>!_disposed&&IsCurrent&&IsLoaded&&IsVisible&&IsEnabled&&lease.Generation==_generation
        &&ReferenceEquals(_session,lease.Owner)&&ReferenceEquals(_frame,lease.Frame)&&(!exactStamp||lease.Owner.Stamp==lease.Stamp);
    private bool TryCaptureMenu(out MenuLease lease)
    {
        lease=null!;if(_capturingMenu||_disposed||!IsCurrent||!IsLoaded||!IsVisible||!IsEnabled||_session is not {} session||_frame is not {} frame)return false;
        _capturingMenu=true;
        try
        {
            var captured=new MenuLease(session,frame,_generation,session.Stamp);
            if(!session.IsCurrent||!Still(captured)||_menuStamp is not null&&(!ReferenceEquals(_menuOwner,session)||_menuGeneration!=_generation||_menuStamp!=captured.Stamp))return false;
            lease=captured;return true;
        }
        finally{_capturingMenu=false;}
    }
    private void InitializeMenus()
    {
        NativeView.NativeRoiMenus=new(
            roi=>TryCaptureMenu(out var lease)&&roi is NativeRoiProjection item&&lease.Owner.CanDelete(new[]{item})&&Still(lease),
            roi=>{if(TryCaptureMenu(out var lease)&&roi is NativeRoiProjection item)lease.Owner.TryDelete(new[]{item},()=>Still(lease,false));},
            roi=>TryCaptureMenu(out var lease)&&roi is NativeRoiProjection item&&lease.Owner.CanCopy(item)&&Still(lease),
            roi=>
            {
                if(!TryCaptureMenu(out var lease)||roi is not NativeRoiProjection item)return;
                if(lease.Owner.TryCopy(item,out var copied)&&Still(lease,false))
                {_clipboard=copied;CommandManager.InvalidateRequerySuggested();}
            },
            ()=>TryCaptureMenu(out var lease)&&lease.Owner.CanPaste(_clipboard)&&Still(lease),
            point=>{if(TryCaptureMenu(out var lease)&&_clipboard is {} copied)lease.Owner.TryPaste(copied,new(point.X,point.Y),()=>Still(lease,false));});
        EnsureCurrent();NativeView.ContextMenuOpening+=OnMenuOpening;NativeView.IsVisibleChanged+=OnMenuVisibility;
    }
    private void OnMenuOpening(object sender,ContextMenuEventArgs args)
    {
        RetireMenuLease();
        if(!TryCaptureMenu(out var lease)){args.Handled=true;return;}
        _menuOwner=lease.Owner;_menuStamp=lease.Stamp;_menuGeneration=lease.Generation;
        var target=args.OriginalSource as DependencyObject;
        while(target is not null)
        {
            if(ContextMenuService.GetContextMenu(target) is {} menu)
            {
                _menuSurface=menu;
                RoutedEventHandler closed=null!;
                closed=(_,_)=>{menu.Closed-=closed;if(ReferenceEquals(_menuSurface,menu)&&ReferenceEquals(_menuClosed,closed))RetireMenuLease();};
                _menuClosed=closed;menu.Closed+=closed;break;
            }
            target=target is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(target):LogicalTreeHelper.GetParent(target);
        }
    }
    private void RetireMenuLease()
    {
        var surface=_menuSurface;var closed=_menuClosed;_menuSurface=null;_menuClosed=null;_menuOwner=null;_menuStamp=null;
        if(surface is not null&&closed is not null)surface.Closed-=closed;
    }
    private void OnMenuVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!IsVisible)ClearMenuState();}
    private void ClearMenuState()
    {
        var menus=Descendants<FrameworkElement>(NativeView).Prepend(NativeView).Select(view=>view.ContextMenu).Where(menu=>menu?.IsOpen==true).Distinct().ToArray();
        var generation=_generation;_clipboard=null;RetireMenuLease();
        NativeRoiCallbacks.Run(menus.Select(menu=>(Action)(()=>{if(generation==_generation&&_owner.MayClearRoiCallbacks(this))menu!.IsOpen=false;})));
    }
    private void RetireMenus()
    {
        // A retained original command must never resume direct collection mutation.
        if(_owner.MayClearRoiCallbacks(this))NativeView.NativeRoiMenus=new(_=>false,_=>{},_=>false,_=>{},()=>false,_=>{});
        NativeView.ContextMenuOpening-=OnMenuOpening;NativeView.IsVisibleChanged-=OnMenuVisibility;ClearMenuState();
    }
}
