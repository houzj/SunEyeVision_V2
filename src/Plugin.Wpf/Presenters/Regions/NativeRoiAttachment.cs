using System.Collections;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using System.Collections.ObjectModel;
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
internal sealed partial class NativeRoiAttachment : INativeRoiEditLease
{
    private NativeRoiSession? _session;
    private NativeResultFrameReceipt? _frame;
    private Action? _changed;
    private Action<RoiDrawRequest>? _drawRequested,_drawCancelled;
    private NativeRoiSession? _drawOwner;
    private RoiDrawRequest? _drawRequest;
    private NativeRoiDrawing? _drawing;
    private long _generation;
    private System.Windows.Threading.DispatcherOperation? _designRefresh;
    private bool _disposed,_projecting,_projectAgain;
    private RoiEditStamp? _shapeStamp;
    private (int Width,int Height,double Ratio,double Thickness)? _shapeContext;
    private System.Windows.Threading.DispatcherOperation? _shapeRefresh;
    private readonly NativeResultViewport _owner;
    private readonly Func<bool> _current;
    internal NativeResultFrameReceipt Receipt {get;}
    internal NativeRoiSession? Session => _session;
    private ImageViewBox NativeView => (ImageViewBox)_owner.View;
    private System.Windows.Threading.Dispatcher Dispatcher => NativeView.Dispatcher;
    private bool IsLoaded => NativeView.IsLoaded;
    private bool IsVisible => NativeView.IsVisible;
    private bool IsEnabled => NativeView.IsEnabled;
    public bool IsCurrent
    {
        get
        {
            if(_disposed||!_owner.OwnsRoi(this)||!_owner.IsFrameCurrent(Receipt))return false;
            var valid=_current();
            return valid&&!_disposed&&_owner.OwnsRoi(this)&&_owner.IsFrameCurrent(Receipt)&&!_disposed&&_owner.OwnsRoi(this);
        }
    }
    public bool HasPendingGesture
    {
        get
        {
            if(!IsCurrent)return false;
            var pending=_drawing is not null||_drawRequest is not null||NativeView.IsDrawingOfROI||NativeView.IsDragROI
                ||_session?.ActiveDraw is not null||_session?.Items.Any(item=>item.HasPending)==true;
            return pending&&IsCurrent;
        }
    }
    public void CancelGesture()
    {
        Dispatcher.VerifyAccess();if(_disposed||!_owner.OwnsRoi(this))return;
        var session=_session;var generation=ROIControlBase.GestureGeneration(NativeView);var cancel=CaptureInteractionCancellation();
        bool Current()=>!_disposed&&ReferenceEquals(session,_session)&&generation==ROIControlBase.GestureGeneration(NativeView)
            &&_owner.OwnsRoi(this)&&_owner.MayCancelRoiSession(this,session);
        NativeRoiCallbacks.Run(new Action[]{()=>{if(_owner.OwnsRoi(this))CancelDrawing(true);},
            cancel,()=>{if(Current())session?.CancelPending(Current);}});
    }
    internal NativeRoiAttachment(NativeResultViewport owner,NativeResultFrameReceipt receipt,Func<bool> current)
    { _owner=owner;Receipt=receipt;_current=current; }
    private void EnsureCurrent()
    {if(!IsCurrent)throw new NativeRoiLeaseRetiredException("ROI交互安装已被新会话替换或失效。");}
    internal void Initialize()
    {
        try
        {
        EnsureCurrent();
        NativeView.Loaded+=OnLoaded;NativeView.Unloaded+=OnUnloaded;
        NativeView.SizeChanged+=OnDesignSize;
        NativeView.PropertyChanged+=OnNativeProjectionChanged;
        NativeView.RoiSelectedCommand=new SelectionCommand(this);
        EnsureCurrent();
        NativeView.ShapeHitTestVisible=false;
        EnsureCurrent();
        NativeView.NativeDrawStart=StartDrawing;NativeView.NativeDrawComplete=CompleteDrawing;NativeView.NativeDrawCancel=()=>CancelDrawing(true);
        NativeView.NativeDrawCaptureLost=source=>{if(_drawing is {} drawing&&!drawing.IsSuspended&&ReferenceEquals(source,drawing.Page)&&!ReferenceEquals(Mouse.Captured,drawing.Page))CancelDrawing(true);};
        NativeView.NativeRoiCaptureCancelEdit=context=>
        {
            if(context is not NativeRoiProjection item||_session is not {} session)return ()=>{};
            var stamp=session.Stamp;
            return ()=>
            {
                var generation=ROIControlBase.GestureGeneration(NativeView);
                bool Current()=>ReferenceEquals(session,_session)&&session.Stamp==stamp
                    &&generation==ROIControlBase.GestureGeneration(NativeView);
                if(!Current()||!session.Owns(item)||!Current()||!_owner.MayCancelRoiSession(this,session)||!Current())return;
                if(item.HasPending&&item.PendingStamp==stamp)item.Cancel();
            };
        };
        NativeView.AddHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture),true);
        NativeView.AddHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled),true);
        NativeView.PreviewMouseDown+=OnPreviewMouse;NativeView.PreviewKeyDown+=OnKey;
        InitializeMenus();
        EnsureCurrent();
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    internal void Bind(NativeRoiSession? session)
    {
        Dispatcher.VerifyAccess();if(_disposed)throw new ObjectDisposedException(nameof(NativeRoiAttachment));
        var generation=++_generation;var previous=_session;_session=null;
        if(previous is not null)
        {
            if(_changed is not null)previous.Changed-=_changed;
            if(_drawRequested is not null)previous.DrawRequested-=_drawRequested;
            if(_drawCancelled is not null)previous.DrawCancelled-=_drawCancelled;
        }
        _changed=null;_drawRequested=null;_drawCancelled=null;
        NativeRoiCallbacks.Run([()=>{if(generation==_generation)ClearMenuState();},()=>{if(generation==_generation)CancelDrawing(true);},
            ()=>{if(generation==_generation)previous?.CancelPending(()=>_owner.MayCancelRoiSession(this,previous));},()=>{if(generation==_generation)ReleaseFrame();}]);
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
        if(IsLoaded||System.ComponentModel.DesignerProperties.GetIsInDesignMode(NativeView))AttachFrame();
    }
    private void PrepareDrawing(NativeRoiSession session,RoiDrawRequest request)
    {
        if(_disposed||!IsCurrent||!IsLoaded||_frame?.Frame!=request.Stamp.Frame||!ReferenceEquals(session.ActiveDraw,request))
        {if(ReferenceEquals(session.ActiveDraw,request)&&_owner.MayCancelRoiSession(this,session))session.CancelDraw();return;}
        var generation=_generation;CancelDrawing(false);
        if(_disposed||!IsCurrent||generation!=_generation||!ReferenceEquals(session,_session)||!session.IsCurrent||!ReferenceEquals(session.ActiveDraw,request)||_frame?.Frame!=request.Stamp.Frame)return;
        _drawOwner=session;_drawRequest=request;
        var type=request.Kind switch
        {
            "rectangle"=>typeof(INormalizedRectROI),"annulus"=>typeof(INormalizedCircleROI),
            "sector"=>typeof(INormalizedSectorROI),"polygon"=>typeof(INormalizedPolygonROI),_=>throw new NotSupportedException()
        };
        NativeView.PrepareNativeRoi(type,()=>!_disposed&&IsCurrent&&generation==_generation&&ReferenceEquals(_session,session)&&ReferenceEquals(_drawRequest,request)&&ReferenceEquals(session.ActiveDraw,request)&&IsCurrent&&generation==_generation&&ReferenceEquals(session.ActiveDraw,request));
    }
    private bool StartDrawing(ROIItemsPage page,System.Windows.Point start)
    {
        var session=_drawOwner;var request=_drawRequest;var generation=_generation;
        if(_disposed||!IsCurrent||session is null||request is null||!ReferenceEquals(session,_session)||!ReferenceEquals(session.ActiveDraw,request)
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
                &&ReferenceEquals(_session,session)&&session.IsCurrent&&session.Stamp==request.Stamp&&ReferenceEquals(session.ActiveDraw,request)&&IsCurrent&&generation==_generation&&ReferenceEquals(_drawRequest,request)&&session.Stamp==request.Stamp&&ReferenceEquals(session.ActiveDraw,request),
            ()=>{if(ReferenceEquals(_drawing,drawing))CancelDrawing(true);},
            error=>{if(ReferenceEquals(_drawing,drawing)){CancelDrawing(false);session.DrawStatus(request,error,true);}},
            active=>{if(ReferenceEquals(_drawing,drawing)&&generation==_generation&&_owner.MayClearRoiCallbacks(this))NativeView.IsDrawingOfROI=active;},maximum);
        if(_disposed||!IsCurrent||generation!=_generation||!ReferenceEquals(_drawRequest,request)){drawing.Dispose();return false;}
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
        return !_disposed&&IsCurrent&&ReferenceEquals(session,_session)&&session?.IsCurrent==true&&IsCurrent&&session.CompleteDraw(request!,completion.Geometry);
    }
    private void CancelDrawing(bool cancelSession)
    {
        var drawing=_drawing;var session=_drawOwner;var request=_drawRequest;
        _drawing=null;_drawOwner=null;_drawRequest=null;
        if(drawing is null&&request is null)return;
        // Retire the old capture before native DP notifications can start another lease on the same page.
        var generation=_generation;
        NativeRoiCallbacks.Run(new Action[]{()=>drawing?.Dispose(),()=>NativeView.UnprepareNativeRoi(()=>_owner.MayClearRoiCallbacks(this)&&generation==_generation&&_drawRequest is null&&_drawing is null),
            ()=>{if(cancelSession&&ReferenceEquals(session?.ActiveDraw,request))session!.CancelDraw();}});
    }
    private void OnLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,NativeView))AttachFrame();}
    private void AttachFrame()
    {
        if(_disposed||_frame is not null||_session?.IsCurrent!=true||!IsCurrent)return;
        if(Receipt.Frame!=_session.Frame)throw new InvalidOperationException("视口和参数不属于同一物理帧。");
        _frame=Receipt;OnChanged();QueueDesignRefresh();
    }
    internal void RefreshProjection() { if(!_disposed&&IsCurrent) { AttachFrame();OnChanged(); } }
    private void OnDesignSize(object sender,SizeChangedEventArgs args){QueueDesignRefresh();QueueShapeRefresh();}
    private void OnNativeProjectionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
    {if(args.PropertyName is "ControlToImageRadio" or "NeedRefreshShapes")QueueShapeRefresh();}
    private void QueueShapeRefresh()
    {
        if(_disposed||_frame is null||_session?.IsCurrent!=true||!_session.Items.Any(item=>item is NativeInheritedAnnulusProjection))return;
        _shapeRefresh?.Abort();var generation=_generation;var frame=_frame;
        _shapeRefresh=Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(()=>
        {
            _shapeRefresh=null;
            if(!_disposed&&generation==_generation&&ReferenceEquals(_frame,frame)&&_session?.IsCurrent==true)OnChanged();
        }));
    }
    private void QueueDesignRefresh()
    {
        if(_disposed||_frame is null||!System.ComponentModel.DesignerProperties.GetIsInDesignMode(NativeView))return;
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
        if(_disposed)return;
        if(_projecting){_projectAgain=true;return;}
        if(_session?.IsCurrent!=true||!IsCurrent){_owner.DetachRoi(this);return;}
        _projecting=true;
        try
        {
            var session=_session;var generation=_generation;var frame=_frame;var snapshotStamp=session.Stamp;
            bool Current()=>!_disposed&&generation==_generation&&ReferenceEquals(_session,session)&&ReferenceEquals(_frame,frame)&&session.IsCurrent&&session.Stamp==snapshotStamp&&IsCurrent;
            if(frame is null||!Current())return;
            // Never lend the live mixed collection to the original ROI template converter:
            // editor layout can run between Rows.Reset and Session.Changed during a type switch.
            // Original ROIItemsPage.InnerROIList requires a generic collection, not an array.
            IList rows=Array.AsReadOnly(session.Items.Where(item=>item is not NativeInheritedAnnulusProjection).ToArray());
            var sameRows=NativeView.ROIList is IList existing&&existing.Cast<object>().SequenceEqual(rows.Cast<object>());
            if(!sameRows)
                if(!_owner.SetRoiRows(this,rows))return;
            if(!Current())return;
            NativeView.UpdateLayout();
            if(!Current())return;
            var page=Descendants<ShapeItemsPage>(NativeView).SingleOrDefault();
            var context=(Width:page?.ImageSourceWidth??0,Height:page?.ImageSourceHeight??0,Ratio:page?.ControlToImageRadio??0,Thickness:page?.ThicknessScale??0);
            if(context.Width>0&&context.Height>0&&double.IsFinite(context.Ratio)&&context.Ratio>0&&double.IsFinite(context.Thickness)&&context.Thickness>0&&
                (_shapeStamp!=snapshotStamp||_shapeContext!=context))
            {
                // The original page handles Add/Reset only. Replace once; never mutate an active shape collection.
                if(!_owner.SetRoiShapes(this,session.Items.OfType<NativeInheritedAnnulusProjection>().Where(item=>item.Visible).Cast<object>().ToArray()))return;
                if(!Current())return;
                _shapeStamp=snapshotStamp;_shapeContext=context;
            }
            NativeView.UpdateLayout();
            if(!Current())return;
            foreach(var roi in Descendants<RectROI>(NativeView))
            {
                // Select the original base template explicitly for normalized derived native controls.
                if(!Current())return;
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(RectROI));
                if(!Current())return;
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                if(!Current())return;roi.ApplyTemplate();if(!Current())return;roi.RefreshGeometryLayout();if(!Current())return;
            }
            foreach(var roi in Descendants<SectorROI>(NativeView))
            {
                if(!Current())return;
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(SectorROI));
                if(!Current())return;
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                if(!Current())return;roi.ApplyTemplate();if(!Current())return;roi.RefreshGeometryLayout();if(!Current())return;
            }
            foreach(var roi in Descendants<CircleROI>(NativeView))
            {
                if(!Current())return;
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(CircleROI));
                if(!Current())return;
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                if(!Current())return;roi.ApplyTemplate();if(!Current())return;roi.RefreshGeometryLayout();if(!Current())return;
            }
            foreach(var roi in Descendants<PolygonROI>(NativeView))
            {
                if(!Current())return;
                roi.SetResourceReference(FrameworkElement.StyleProperty,typeof(PolygonROI));
                if(!Current())return;
                roi.SetBinding(UIElement.IsHitTestVisibleProperty,new Binding(nameof(NativeRoiProjection.Enabled)));
                if(!Current())return;roi.ApplyTemplate();if(!Current())return;roi.RefreshGeometryLayout();if(!Current())return;
            }
            foreach(var list in Descendants<ListView>(NativeView).Where(list=>ReferenceEquals(list.ItemsSource,NativeView.ROIList)))
                {if(!Current())return;list.SelectedItem=session.CurROIData;if(!Current())return;}
        }
        finally{_projecting=false;if(_projectAgain){_projectAgain=false;OnChanged();}}
    }
    private void OnKey(object sender,KeyEventArgs args)
    {
        if(args.Key==Key.Escape){CancelGesture();args.Handled=true;return;}
        if(Keyboard.Modifiers==ModifierKeys.Control&&args.Key is Key.Z or Key.Y
            &&Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase&&TryCaptureMenu(out var lease))
        {if(args.Key==Key.Z)lease.Owner.TryUndo(()=>Still(lease,false));else lease.Owner.TryRedo(()=>Still(lease,false));args.Handled=true;}
    }
    private void CancelInteraction()=>CaptureInteractionCancellation()();
    private Action CaptureInteractionCancellation()
    {
        var generation=ROIControlBase.GestureGeneration(NativeView);var wasDragging=NativeView.IsDragROI;
        var cancellations=Descendants<ROIControlBase>(NativeView)
            .Where(roi=>roi is RectROI or SectorROI or CircleROI or PolygonROI)
            .Select(roi=>roi.CaptureGestureCancellation()).ToArray();
        return ()=>NativeRoiCallbacks.Run(cancellations.Select(cancel=>(Action)(()=>{if(_owner.MayClearRoiCallbacks(this))cancel();}))
            .Append(()=>{if(wasDragging&&generation==ROIControlBase.GestureGeneration(NativeView)&&_owner.MayClearRoiCallbacks(this))NativeView.IsDragROI=false;}));
    }
    private void OnPreviewMouse(object sender,MouseButtonEventArgs args)
    {if(!IsCurrent||_session?.PrepareEditor()!=true||!IsCurrent)args.Handled=true;}
    private void OnPolygonCancelled(object sender,RoutedEventArgs args)
    {if(!_disposed&&args.OriginalSource is PolygonROI {DataContext:NativePolygonRoiProjection item}&&_session is {} session&&session.Owns(item))
        CancelGesture();}
    private void OnLostCapture(object sender,MouseEventArgs args)
    {
        if(_disposed)return;
        if(ReferenceEquals(Mouse.Captured,args.OriginalSource))return;
        if(!Descendants<RectROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<SectorROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<CircleROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource))&&
           !Descendants<PolygonROI>(NativeView).Any(roi=>ReferenceEquals(roi.InteractionHandle,args.OriginalSource)))return;
        var session=_session;var generation=ROIControlBase.GestureGeneration(NativeView);
        bool Current()=>!_disposed&&ReferenceEquals(session,_session)&&generation==ROIControlBase.GestureGeneration(NativeView)
            &&_owner.MayCancelRoiSession(this,session);
        NativeRoiCallbacks.Run([CancelInteraction,()=>{if(Current())session?.CancelPending(Current);}]);
    }
    private void OnUnloaded(object sender,RoutedEventArgs args)
    {
        if(!ReferenceEquals(args.OriginalSource,NativeView))return;
        var generation=_generation;var session=_session;
        NativeRoiCallbacks.Run([()=>session?.CancelPending(()=>_owner.MayCancelRoiSession(this,session)),()=>{if(generation==_generation)ReleaseFrame();}]);
    }
    private void ReleaseFrame()
    {
        var generation=_generation;_frame=null;
        var designRefresh=_designRefresh;_designRefresh=null;
        var shapeRefresh=_shapeRefresh;_shapeRefresh=null;
        bool OwnsCleanup()=>generation==_generation&&_frame is null;
        NativeRoiCallbacks.Run([()=>designRefresh?.Abort(),()=>shapeRefresh?.Abort(),()=>{if(OwnsCleanup())ClearMenuState();},
            ()=>{if(OwnsCleanup())CancelDrawing(true);},()=>{if(OwnsCleanup())CancelInteraction();},
            ()=>{if(OwnsCleanup()&&!_disposed)_owner.SetRoiRows(this,Array.AsReadOnly(Array.Empty<object>()));},
            ()=>{if(OwnsCleanup()){_shapeStamp=null;_shapeContext=null;if(!_disposed)_owner.SetRoiShapes(this,Array.Empty<object>());}}]);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_generation++;
        NativeView.Loaded-=OnLoaded;NativeView.Unloaded-=OnUnloaded;NativeView.PreviewMouseDown-=OnPreviewMouse;NativeView.PreviewKeyDown-=OnKey;
        NativeView.RemoveHandler(Mouse.LostMouseCaptureEvent,new MouseEventHandler(OnLostCapture));
        NativeView.RemoveHandler(PolygonROI.InteractionCancelledEvent,new RoutedEventHandler(OnPolygonCancelled));
        NativeView.SizeChanged-=OnDesignSize;
        NativeView.PropertyChanged-=OnNativeProjectionChanged;
        var previous=_session;_session=null;
        if(previous is not null)
        {
            if(_changed is not null)previous.Changed-=_changed;
            if(_drawRequested is not null)previous.DrawRequested-=_drawRequested;
            if(_drawCancelled is not null)previous.DrawCancelled-=_drawCancelled;
        }
        _changed=null;_drawRequested=null;_drawCancelled=null;
        NativeRoiCallbacks.Run([RetireMenus,()=>previous?.CancelPending(()=>_owner.MayCancelRoiSession(this,previous)),ReleaseFrame,()=>{if(_owner.MayClearRoiCallbacks(this))NativeView.RoiSelectedCommand=null;},
            ()=>{if(_owner.MayClearRoiCallbacks(this)){NativeView.NativeDrawStart=(_,_)=>false;NativeView.NativeDrawComplete=_=>false;NativeView.NativeDrawCancel=()=>{};NativeView.NativeDrawCaptureLost=_=>{};NativeView.NativeRoiCaptureCancelEdit=null;}},()=>_owner.DetachRoi(this)]);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var nested in Descendants<T>(child))yield return nested;}
    }
    private sealed class SelectionCommand(NativeRoiAttachment view):ICommand
    {
        public bool CanExecute(object? parameter)=>!view._disposed&&view.IsCurrent&&view._session?.IsCurrent==true&&view.IsCurrent;
        public void Execute(object? parameter)
        {
            if(CanExecute(parameter)&&parameter is RoiWithTagEventArgs {Roi:NativeRoiProjection item}&&view._session!.Owns(item))
                {view._session!.Select(item.Group,item.Id);view.OnChanged();}
        }
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}
