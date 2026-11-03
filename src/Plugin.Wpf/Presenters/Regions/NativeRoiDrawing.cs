using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

internal sealed record NativeRoiDrawCompletion(RoiDrawRequest Request,RecordValue Geometry);

/// <summary>Selected original four-shape mouse algorithms and substitute visuals, with instance-owned leases.
/// Coordinates are page units divided by the original display ratio, never divided by image dimensions twice.</summary>
internal sealed class NativeRoiDrawing : IDisposable
{
    private sealed class CaptureOwner {internal NativeRoiDrawing? Value;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ROIItemsPage,CaptureOwner> Captures=new();
    private readonly ROIItemsPage _page;
    private readonly RoiDrawRequest _request;
    private readonly Func<bool> _current;
    private readonly Action _cancel;
    private readonly Action<string> _reject;
    private readonly Action<bool> _drawingState;
    private NativeMessageBox? _warningDialog;
    private readonly Point _start;
    private readonly double _ratio;
    private readonly int _width,_height,_maximumPoints;
    private Canvas? _host;
    private DependencyPropertyDescriptor? _template;
    internal ROIItemsPage Page=>_page;
    private bool _disposed,_ended,_moved,_onePoint,_modal;
    internal bool IsSuspended=>_modal;
    internal ROISubstitute Substitute {get;}
    internal NativeRoiDrawing(ROIItemsPage page,RoiDrawRequest request,Point start,Func<bool> current,Action cancel,Action<string> reject,Action<bool> drawingState,int maximumPoints)
    {
        _page=page;_request=request;_start=start;_current=current;_cancel=cancel;_reject=reject;_drawingState=drawingState;
        _ratio=page.ControlToImageRadio;_width=page.ImageWidth;_height=page.ImageHeight;_maximumPoints=maximumPoints;
        Substitute=request.Kind switch
        {
            "rectangle"=>new RectROISubstitute{SubstituteRect=new Rect(start,new Size(0,0))},
            "annulus"=>new CircleROISubstitute{CenterPoint=start,Radius=0},
            "sector"=>new SectorROISubstitute{CenterPoint=start,Radius=0,StartAngle=0},
            "polygon"=>new PolygonROISubstitute{Points=new PointCollection{start,start}},
            _=>throw new NotSupportedException()
        };
        Substitute.IsHitTestVisible=false;
        Substitute.SetBinding(ROIControlFrame.ThicknessScaleProperty,new Binding(nameof(page.ThicknessScale)){Source=page});
        Substitute.SetBinding(ROIControlFrame.ControlToImageRadioProperty,new Binding(nameof(page.ControlToImageRadio)){Source=page});
    }
    private bool LeaseAlive=>!_disposed&&!_ended&&_current()&&_page.IsLoaded&&_page.ImageWidth==_width&&_page.ImageHeight==_height
        &&(_host is null||ReferenceEquals(_page.SubstituteHost,_host))&&double.IsFinite(_ratio)&&_ratio>0&&_page.ControlToImageRadio==_ratio;
    private bool Alive=>!_modal&&LeaseAlive;
    private void AttachWarning(NativeMessageBox window)
    {_warningDialog=window;window.LayoutUpdated+=OnWarningLayout;}
    private void OnWarningLayout(object? sender,EventArgs args)
    {if(!_disposed&&_warningDialog is {} window)NativeTextInheritance.Restore(window);}
    private void DetachWarning(NativeMessageBox? window)
    {if(window is not null)window.LayoutUpdated-=OnWarningLayout;}
    private bool Capture()
    {
        var owner=Captures.GetValue(_page,_=>new());owner.Value=this;
        if(_page.CaptureMouse())return ReferenceEquals(owner.Value,this)&&ReferenceEquals(Mouse.Captured,_page);
        if(ReferenceEquals(owner.Value,this))owner.Value=null;
        return false;
    }
    private void ReleaseCapture()
    {
        if(!Captures.TryGetValue(_page,out var owner)||!ReferenceEquals(owner.Value,this))return;
        owner.Value=null;if(ReferenceEquals(Mouse.Captured,_page))_page.ReleaseMouseCapture();
    }
    internal bool Start()
    {
        if(!Alive)return false;
        _page.ApplyTemplate();_host=_page.SubstituteHost;if(_host is null)return false;
        if(!Capture()){_reject("原绘制画布未能取得鼠标捕获。");return false;}
        if(!Alive||!ReferenceEquals(Mouse.Captured,_page)){Dispose();return false;}
        try
        {
            _host.Children.Add(Substitute);
            if(!Alive||!ReferenceEquals(Mouse.Captured,_page)){Dispose();return false;}
            _template=DependencyPropertyDescriptor.FromProperty(Control.TemplateProperty,_page.GetType());
            _template.AddValueChanged(_page,OnTemplate);
            _page.MouseMove+=OnMove;_page.MouseLeftButtonUp+=OnUp;
            _page.MouseLeftButtonDown+=OnDown;_page.MouseRightButtonUp+=OnRight;
            _page.LostMouseCapture+=OnLost;_page.Unloaded+=OnUnloaded;
            return true;
        }
        catch{Dispose();throw;}
    }
    private void OnTemplate(object? sender,EventArgs args){if(!_disposed)_cancel();}
    private void Guard(Action action)
    {
        try{action();}
        catch(Exception error)
        {
            NativeRoiCallbacks.Run(new Action[]{()=>System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(),()=>{if(!_disposed)_cancel();}});
        }
    }
    private void OnMove(object sender,MouseEventArgs args)=>Move(args.GetPosition(_page),args.LeftButton==MouseButtonState.Pressed);
    private void OnUp(object sender,MouseButtonEventArgs args){if(_request.Kind!="polygon"){Release();args.Handled=true;}}
    private void OnDown(object sender,MouseButtonEventArgs args){if(_request.Kind=="polygon"){Click(args.ClickCount);args.Handled=true;}}
    private void OnRight(object sender,MouseButtonEventArgs args){if(_request.Kind=="polygon"){Back();args.Handled=true;}}
    private void OnLost(object sender,MouseEventArgs args){if(!_disposed&&!_ended&&!_modal&&!_page.IsMouseCaptured)_cancel();}
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_page))_cancel();}
    internal void Move(Point point,bool pressed)=>Guard(()=>MoveCore(point,pressed));
    private void MoveCore(Point point,bool pressed)
    {
        if(_modal)return;
        if(!Alive){_cancel();return;}
        if(pressed)_moved=true;
        switch(Substitute)
        {
            case RectROISubstitute rect:rect.SubstituteRect=new Rect(_start,point);break;
            case CircleROISubstitute circle:circle.Radius=(point-_start).Length;break;
            case SectorROISubstitute sector:sector.Radius=(point-_start).Length;break;
            case PolygonROISubstitute polygon:polygon.Points[polygon.Points.Count-1]=point;break;
        }
    }
    internal void Click(int clickCount)=>Guard(()=>ClickCore(clickCount));
    private void ClickCore(int clickCount)
    {
        if(_modal)return;
        if(!Alive){_cancel();return;}
        if(Substitute is not PolygonROISubstitute polygon)return;
        if(clickCount==1)
        {
            if(polygon.Points.Count>_maximumPoints){WarnLimit(polygon);return;}
            _onePoint=true;polygon.Points.Add(polygon.Points.Last());
        }
        else if(_onePoint&&polygon.Points.Count>3)
        {
            _onePoint=false;polygon.Points.RemoveAt(polygon.Points.Count-1);Finish();
        }
    }
    internal void Back()=>Guard(()=>BackCore());
    private void BackCore()
    {
        if(_modal)return;
        if(!Alive){_cancel();return;}
        if(Substitute is not PolygonROISubstitute polygon)return;
        if(polygon.Points.Count<=2){_cancel();return;}
        polygon.Points.RemoveAt(polygon.Points.Count-2);
    }
    internal void Release()=>Guard(()=>ReleaseCore());
    private void ReleaseCore(){if(_modal)return;if(!Alive){_cancel();return;}if(!_moved&&!(Substitute is RectROISubstitute rect&&(rect.SubstituteRect.Width>0||rect.SubstituteRect.Height>0))
            &&!(Substitute is CircleROISubstitute circle&&circle.Radius>0)&&!(Substitute is SectorROISubstitute sector&&sector.Radius>0))
            {_cancel();return;}Finish();}
    private void WarnLimit(PolygonROISubstitute polygon)
    {
        var owner=Window.GetWindow(_page);
        if(owner is null||!owner.IsVisible){_cancel();return;}
        _modal=true;
        CancelEventHandler closing=(_,args)=>{if(!args.Cancel)_cancel();};
        owner.Closing+=closing;
        try
        {
            _drawingState(false);
            if(!LeaseAlive){_cancel();return;}
            ReleaseCapture();
            if(!LeaseAlive){_cancel();return;}
            NativeMessageBox.ShowOwnedWarning(owner,AppLocalizationService.GetLang("多边形点数不超过")+" "+_maximumPoints,
                AttachWarning,()=>LeaseAlive);
            if(!LeaseAlive){_cancel();return;}
            polygon.Points.RemoveAt(polygon.Points.Count-2);
            _drawingState(true);
            if(!LeaseAlive){_cancel();return;}
            if(!Capture()||!LeaseAlive)_cancel();
        }
        finally{owner.Closing-=closing;DetachWarning(_warningDialog);_warningDialog=null;_modal=false;}
    }
    private void Finish()
    {
        if(!Alive){_cancel();return;}
        RecordValue geometry;
        try
        {
            Point2 Pixel(Point point)=>new(point.X/_ratio,point.Y/_ratio);
            if(Substitute is RectROISubstitute rect)
            {
                var value=rect.SubstituteRect;
                geometry=RoiGeometryEditing.Create("rectangle",Pixel(value.TopLeft),Pixel(value.BottomRight));
            }
            else if(Substitute is PolygonROISubstitute polygon)
                geometry=RoiGeometryEditing.Create("polygon",default,default,polygon.Points.Select(Pixel).ToArray());
            else
            {
                var radius=Substitute is CircleROISubstitute circle?circle.Radius:((SectorROISubstitute)Substitute).Radius;
                var center=Pixel(_start);
                var values=new Dictionary<string,object?>{{"kind",_request.Kind},{"center",RoiGeometryEditing.Point(center)},
                    {"inner-radius",0d},{"outer-radius",radius/_ratio},{"start-radians",0d},
                    {"end-radians",_request.Kind=="sector"?Math.PI/2:2*Math.PI}};
                geometry=RoiGeometryEditing.Checked(values);
            }
        }
        catch(Exception error)when(error is ArgumentException or OverflowException){_reject(error.Message);return;}
        _ended=true;
        try{_page.CompleteDrawSubstitute(new NativeRoiDrawCompletion(_request,geometry));}
        finally{Dispose();}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;var warning=_warningDialog;_warningDialog=null;
        NativeRoiCallbacks.Run(new Action[]{
            ()=>{_page.MouseMove-=OnMove;_page.MouseLeftButtonUp-=OnUp;_page.MouseLeftButtonDown-=OnDown;_page.MouseRightButtonUp-=OnRight;_page.LostMouseCapture-=OnLost;_page.Unloaded-=OnUnloaded;},
            ()=>{_template?.RemoveValueChanged(_page,OnTemplate);_template=null;},
            ()=>_host?.Children.Remove(Substitute),
            ReleaseCapture,()=>DetachWarning(warning),()=>warning?.Close()});
    }
}
