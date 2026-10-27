using System.Collections;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;
using SunEyeVision.Plugin.Wpf.Regions;
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
    private Action<RoiDrawRequest>? _drawRequested,_drawCancelled;
    private NativeRoiSession? _drawOwner;
    private RoiDrawRequest? _drawRequest;
    private NativeRoiDrawing? _drawing;
    private long _generation;
    private System.Windows.Threading.DispatcherOperation? _designRefresh;
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
        NativeView.SizeChanged+=OnDesignSize;
        NativeView.RoiSelectedCommand=new SelectionCommand(this);
        NativeView.NativeDrawStart=StartDrawing;NativeView.NativeDrawComplete=CompleteDrawing;NativeView.NativeDrawCancel=()=>CancelDrawing(true);
        NativeView.NativeDrawCaptureLost=source=>{if(_drawing is {} drawing&&!drawing.IsSuspended&&ReferenceEquals(source,drawing.Page)&&!ReferenceEquals(Mouse.Captured,drawing.Page))CancelDrawing(true);};
        NativeView.AddHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture),true);
        NativeView.AddHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled),true);
        PreviewMouseDown+=OnPreviewMouse;PreviewKeyDown+=OnKey;
    }
    public void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeRoiViewport));
        var generation=++_generation;var previous=_session;_session=null;
        if(previous is not null)
        {
            if(_changed is not null)previous.Changed-=_changed;
            if(_drawRequested is not null)previous.DrawRequested-=_drawRequested;
            if(_drawCancelled is not null)previous.DrawCancelled-=_drawCancelled;
        }
        _changed=null;_drawRequested=null;_drawCancelled=null;
        NativeRoiCallbacks.Run([()=>CancelDrawing(true),()=>{if(generation==_generation)previous?.CancelPending();},()=>{if(generation==_generation)ReleaseFrame();}]);
        if(_disposed||generation!=_generation)return;
        _session=session;
        if(session is not null)
        {
            _changed=()=>{if(!_disposed&&generation==_generation&&ReferenceEquals(_session,session))OnChanged();};
            session.Changed+=_changed;
            _drawRequested=request=>{if(!_disposed&&generation==_generation&&ReferenceEquals(_session,session))PrepareDrawing(session,request);};
            _drawCancelled=request=>{if(ReferenceEquals(_drawRequest,request))CancelDrawing(false);};
            session.DrawRequested+=_drawRequested;session.DrawCancelled+=_drawCancelled;
        }
        if(IsLoaded||System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))AttachFrame();
    }
    private void PrepareDrawing(NativeRoiSession session,RoiDrawRequest request)
    {
        if(_disposed||!IsLoaded||_frame?.Frame!=request.Stamp.Frame||!ReferenceEquals(session.ActiveDraw,request))
        {session.CancelDraw();return;}
        var generation=_generation;CancelDrawing(false);
        if(_disposed||generation!=_generation||!ReferenceEquals(session,_session)||!session.IsCurrent||!ReferenceEquals(session.ActiveDraw,request)||_frame?.Frame!=request.Stamp.Frame)return;
        _drawOwner=session;_drawRequest=request;
        var type=request.Kind switch
        {
            "rectangle"=>typeof(INormalizedRectROI),"annulus"=>typeof(INormalizedCircleROI),
            "sector"=>typeof(INormalizedSectorROI),"polygon"=>typeof(INormalizedPolygonROI),_=>throw new NotSupportedException()
        };
        NativeView.PrepareNativeRoi(type,()=>!_disposed&&generation==_generation&&ReferenceEquals(_session,session)&&ReferenceEquals(_drawRequest,request)&&ReferenceEquals(session.ActiveDraw,request));
    }
    private bool StartDrawing(ROIItemsPage page,System.Windows.Point start)
    {
        var session=_drawOwner;var request=_drawRequest;var generation=_generation;
        if(_disposed||session is null||request is null||!ReferenceEquals(session,_session)||!ReferenceEquals(session.ActiveDraw,request)
            ||!session.IsCurrent||session.Stamp!=request.Stamp||_frame?.Frame!=request.Stamp.Frame
            ||page.ImageWidth!=session.ImageWidth||page.ImageHeight!=session.ImageHeight)
        {CancelDrawing(true);return false;}
        if(_drawing is not null)return true;
        var maximum=(int)NativeView.GetValue(PolygonROIAttach.CustomMaxPointNumProperty);if(maximum<=0)maximum=32;
        NativeRoiDrawing? drawing=null;
        try
        {
        drawing=new(page,request,start,
            ()=>!_disposed&&generation==_generation&&ReferenceEquals(_drawing,drawing)&&ReferenceEquals(_drawRequest,request)
                &&ReferenceEquals(_session,session)&&session.IsCurrent&&session.Stamp==request.Stamp&&ReferenceEquals(session.ActiveDraw,request),
            ()=>{if(ReferenceEquals(_drawing,drawing))CancelDrawing(true);},
            error=>{if(ReferenceEquals(_drawing,drawing)){CancelDrawing(false);session.DrawStatus(request,error,true);}},
            active=>{if(ReferenceEquals(_drawing,drawing)&&generation==_generation)NativeView.IsDrawingOfROI=active;},maximum);
        if(_disposed||generation!=_generation||!ReferenceEquals(_drawRequest,request)){drawing.Dispose();return false;}
        _drawing=drawing;NativeView.IsDrawingOfROI=true;
        if(drawing.Start())return true;
        if(ReferenceEquals(_drawing,drawing))CancelDrawing(true);return false;
        }
        catch(Exception error){NativeRoiCallbacks.Run(new Action[]{()=>System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(),()=>{if(ReferenceEquals(_drawRequest,request))CancelDrawing(true);else drawing?.Dispose();}});throw;}
    }
    private bool CompleteDrawing(object value)
    {
        if(value is not NativeRoiDrawCompletion completion||!ReferenceEquals(_drawRequest,completion.Request))return false;
        var session=_drawOwner;var request=_drawRequest;
        CancelDrawing(false);
        return !_disposed&&ReferenceEquals(session,_session)&&session?.IsCurrent==true&&session.CompleteDraw(request!,completion.Geometry);
    }
    private void CancelDrawing(bool cancelSession)
    {
        var drawing=_drawing;var session=_drawOwner;var request=_drawRequest;
        _drawing=null;_drawOwner=null;_drawRequest=null;
        if(drawing is null&&request is null)return;
        // Retire the old capture before native DP notifications can start another lease on the same page.
        var generation=_generation;
        NativeRoiCallbacks.Run(new Action[]{()=>drawing?.Dispose(),()=>NativeView.UnprepareNativeRoi(()=>generation==_generation&&_drawRequest is null&&_drawing is null),
            ()=>{if(cancelSession&&ReferenceEquals(session?.ActiveDraw,request))session!.CancelDraw();}});
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
            NativeView.ROIList=(IList)session.Items;OnChanged();QueueDesignRefresh();
        }
        catch{if(ReferenceEquals(_frame,frame))ReleaseFrame();else frame.Dispose();throw;}
    }
    private void OnDesignSize(object sender,SizeChangedEventArgs args)=>QueueDesignRefresh();
    private void QueueDesignRefresh()
    {
        if(_disposed||_frame is null||!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))return;
        _designRefresh?.Abort();var generation=_generation;var frame=_frame;
        _designRefresh=Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>
        {
            if(_disposed||generation!=_generation||!ReferenceEquals(_frame,frame)||_session?.IsCurrent!=true)return;
            _designRefresh=null;
            if(NativeView.ActualWidth<=0||NativeView.ActualHeight<=0)return;
            NativeView.ApplyTemplate();NativeView.UpdateLayout();
            if(_disposed||generation!=_generation||!ReferenceEquals(_frame,frame))return;
            if(NativeView.Source is null)NativeView.Init();
            if(!_disposed&&generation==_generation&&ReferenceEquals(_frame,frame))OnChanged();
        }));
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
    {
        if(!ReferenceEquals(args.OriginalSource,this))return;
        var generation=_generation;var session=_session;
        NativeRoiCallbacks.Run([()=>session?.CancelPending(),()=>{if(generation==_generation)ReleaseFrame();}]);
    }
    private void ReleaseFrame()
    {
        _designRefresh?.Abort();_designRefresh=null;
        var generation=_generation;var frame=_frame;_frame=null;
        bool OwnsCleanup()=>generation==_generation&&_frame is null;
        NativeRoiCallbacks.Run([()=>CancelDrawing(true),()=>{if(OwnsCleanup())CancelInteraction();},
            ()=>{if(OwnsCleanup())NativeView.ROIList=null;},()=>{if(OwnsCleanup())NativeView.ImageDataSource=null;},()=>frame?.Dispose()]);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        Loaded-=OnLoaded;Unloaded-=OnUnloaded;PreviewMouseDown-=OnPreviewMouse;PreviewKeyDown-=OnKey;
        NativeView.RemoveHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture));
        NativeView.RemoveHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled));
        NativeView.SizeChanged-=OnDesignSize;
        var previous=_session;_session=null;
        if(previous is not null)
        {
            if(_changed is not null)previous.Changed-=_changed;
            if(_drawRequested is not null)previous.DrawRequested-=_drawRequested;
            if(_drawCancelled is not null)previous.DrawCancelled-=_drawCancelled;
        }
        _changed=null;_drawRequested=null;_drawCancelled=null;
        NativeRoiCallbacks.Run([()=>previous?.CancelPending(),ReleaseFrame,()=>NativeView.RoiSelectedCommand=null,
            ()=>{NativeView.NativeDrawStart=(_,_)=>false;NativeView.NativeDrawComplete=_=>false;NativeView.NativeDrawCancel=()=>{};NativeView.NativeDrawCaptureLost=_=>{};},()=>Child=null]);
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
