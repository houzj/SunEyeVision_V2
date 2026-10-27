using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>The original ImageViewBox and normalized ROI handles over the same objects as the parameter stack.</summary>
public sealed class NativeRoiViewport : Decorator,IDisposable
{
    private NativeRoiSession? _session;
    private NativeRoiImageFrame? _frame;
    private Action? _changed;
    private long _generation;
    private bool _disposed,_projecting;
    public ImageViewBox NativeView {get;}=new();
    public NativeRoiViewport()
    {
        foreach(var resource in new[]{"Themes/Native/OriginalTheme.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/roiitemspagestyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/subroiitemspagestyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/rectroi/rectroistyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/circleroi/circleroistyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/polygonroi/polygonroistyle.xaml",
            "Themes/Native/Display/VMBaseControls/base/imageview/roi/sectorroi/sectorroistyle.xaml"})
            NativeView.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SunEyeVision.Plugin.Wpf;component/"+resource,UriKind.Relative)});
        Child=NativeView;Loaded+=OnLoaded;Unloaded+=OnUnloaded;
        NativeView.RoiSelectedCommand=new SelectionCommand(this);
        NativeView.AddHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture),true);
        NativeView.AddHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled),true);
        PreviewMouseDown+=OnPreviewMouse;PreviewKeyDown+=OnKey;
    }
    public void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeRoiViewport));
        var generation=++_generation;var previous=_session;_session=null;
        if(previous is not null&&_changed is not null)previous.Changed-=_changed;
        _changed=null;
        NativeRoiCallbacks.Run([()=>previous?.CancelPending(),ReleaseFrame]);
        if(_disposed||generation!=_generation)return;
        _session=session;
        if(session is not null)
        {
            _changed=()=>{if(!_disposed&&generation==_generation&&ReferenceEquals(_session,session))OnChanged();};
            session.Changed+=_changed;
        }
        if(IsLoaded)AttachFrame();
    }
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,this))AttachFrame();}
    private void AttachFrame()
    {
        if(_disposed||_frame is not null||_session?.IsCurrent!=true)return;
        var session=_session;var generation=_generation;
        var frame=new NativeRoiImageFrame(session.Image);
        if(_disposed||generation!=_generation||!ReferenceEquals(_session,session)||!session.IsCurrent||_frame is not null)
        {frame.Dispose();return;}
        if(frame.Frame!=session.Frame){frame.Dispose();throw new InvalidOperationException("视口和参数不属于同一物理帧。");}
        try
        {
            _frame=frame;NativeView.ImageDataSource=frame;
            if(generation!=_generation||!ReferenceEquals(_frame,frame))return;
            NativeView.ROIList=(IList)session.Items;OnChanged();
        }
        catch{if(ReferenceEquals(_frame,frame))ReleaseFrame();else frame.Dispose();throw;}
    }
    private void OnChanged()
    {
        if(_projecting||_disposed)return;
        if(_session?.IsCurrent!=true){ReleaseFrame();return;}
        _projecting=true;
        try
        {
            NativeView.UpdateLayout();
            foreach(var roi in Descendants<RectROI>(NativeView))
            {
                // Select the original base template explicitly for normalized derived native controls.
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(RectROI));
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                roi.ApplyTemplate();roi.RefreshGeometryLayout();
            }
            foreach(var roi in Descendants<SectorROI>(NativeView))
            {
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(SectorROI));
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                roi.ApplyTemplate();roi.RefreshGeometryLayout();
            }
            foreach(var roi in Descendants<CircleROI>(NativeView))
            {
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(CircleROI));
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                roi.ApplyTemplate();roi.RefreshGeometryLayout();
            }
            foreach(var roi in Descendants<PolygonROI>(NativeView))
            {
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(PolygonROI));
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                roi.ApplyTemplate();roi.RefreshGeometryLayout();
            }
            foreach(var list in Descendants<ListView>(NativeView).Where(list=>ReferenceEquals(list.ItemsSource,_session.Items)))
                list.SelectedItem=_session.CurROIData;
        }
        finally{_projecting=false;}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {if(args.Key==Key.Escape){CancelInteraction();_session?.CancelPending();args.Handled=true;}}
    private void CancelInteraction()
    {
        NativeRoiCallbacks.Run(Descendants<RectROI>(NativeView).Select(roi=>(Action)roi.CancelInteraction)
            .Concat(Descendants<SectorROI>(NativeView).Select(roi=>(Action)roi.CancelInteraction))
            .Concat(Descendants<CircleROI>(NativeView).Select(roi=>(Action)roi.CancelInteraction))
            .Concat(Descendants<PolygonROI>(NativeView).Select(roi=>(Action)roi.CancelInteraction)));
    }
    private void OnPreviewMouse(object sender,MouseButtonEventArgs args)
    {if(_session?.PrepareEditor()!=true)args.Handled=true;}
    private void OnPolygonCancelled(object sender,RoutedEventArgs args)
    {if(!_disposed&&args.OriginalSource is PolygonROI {DataContext:NativePolygonRoiProjection item}&&_session?.Owns(item)==true)
        NativeRoiCallbacks.Run([CancelInteraction,_session.CancelPending]);}
    private void OnLostCapture(object sender,MouseEventArgs args)
    {
        if(_disposed)return;
        if(!Descendants<RectROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<SectorROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<CircleROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<PolygonROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource)))return;
        NativeRoiCallbacks.Run([CancelInteraction,()=>_session?.CancelPending()]);
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {if(!ReferenceEquals(args.OriginalSource,this))return;NativeRoiCallbacks.Run([()=>_session?.CancelPending(),ReleaseFrame]);}
    private void ReleaseFrame()
    {
        var frame=_frame;_frame=null;
        NativeRoiCallbacks.Run([CancelInteraction,()=>NativeView.ROIList=null,()=>NativeView.ImageDataSource=null,()=>frame?.Dispose()]);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;PreviewMouseDown-=OnPreviewMouse;PreviewKeyDown-=OnKey;
        NativeView.RemoveHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture));
        NativeView.RemoveHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled));
        var previous=_session;_session=null;
        if(previous is not null&&_changed is not null)previous.Changed-=_changed;_changed=null;
        NativeRoiCallbacks.Run([()=>previous?.CancelPending(),ReleaseFrame,()=>NativeView.RoiSelectedCommand=null,()=>Child=null]);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class SelectionCommand(NativeRoiViewport view):ICommand
    {
        public bool CanExecute(object? parameter)=>!view._disposed&&view._session?.IsCurrent==true;
        public void Execute(object? parameter)
        {
            if(CanExecute(parameter)&&parameter is RoiWithTagEventArgs {Roi:NativeRoiProjection item}&&view._session!.Owns(item))
                {view._session!.Select(item.Group,item.Id);view.OnChanged();}
        }
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}
