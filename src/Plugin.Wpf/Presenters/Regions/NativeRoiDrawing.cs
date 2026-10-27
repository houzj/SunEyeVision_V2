using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

internal sealed record NativeRoiDrawCompletion(RoiDrawRequest Request,RecordValue Geometry);

/// <summary>Selected original four-shape mouse algorithms and substitute visuals, with instance-owned leases.
/// Coordinates are page units divided by the original display ratio, never divided by image dimensions twice.</summary>
internal sealed class NativeRoiDrawing : IDisposable
{
    private readonly ROIItemsPage _page;
    private readonly RoiDrawRequest _request;
    private readonly Func<bool> _current;
    private readonly Action _cancel;
    private readonly Action<string> _reject,_warning;
    private readonly Point _start;
    private readonly double _ratio;
    private readonly int _width,_height,_maximumPoints;
    private Canvas? _host;
    private DependencyPropertyDescriptor? _template;
    internal ROIItemsPage Page=>_page;
    private bool _disposed,_ended,_moved,_onePoint;
    internal ROISubstitute Substitute {get;}
    internal NativeRoiDrawing(ROIItemsPage page,RoiDrawRequest request,Point start,Func<bool> current,Action cancel,Action<string> reject,Action<string> warning,int maximumPoints)
    {
        _page=page;_request=request;_start=start;_current=current;_cancel=cancel;_reject=reject;_warning=warning;
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
    private bool Alive=>!_disposed&&!_ended&&_current()&&_page.IsLoaded&&_page.ImageWidth==_width&&_page.ImageHeight==_height
        &&(_host is null||ReferenceEquals(_page.SubstituteHost,_host))&&double.IsFinite(_ratio)&&_ratio>0&&_page.ControlToImageRadio==_ratio;
    internal bool Start()
    {
        if(!Alive)return false;
        _page.ApplyTemplate();_host=_page.SubstituteHost;if(_host is null)return false;
        if(!_page.CaptureMouse()){_reject("原绘制画布未能取得鼠标捕获。");return false;}
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
    private void OnLost(object sender,MouseEventArgs args){if(!_disposed&&!_ended&&!_page.IsMouseCaptured)_cancel();}
    private void OnUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_page))_cancel();}
    internal void Move(Point point,bool pressed)=>Guard(()=>MoveCore(point,pressed));
    private void MoveCore(Point point,bool pressed)
    {
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
        if(!Alive){_cancel();return;}
        if(Substitute is not PolygonROISubstitute polygon)return;
        if(clickCount==1)
        {
            if(polygon.Points.Count>_maximumPoints){polygon.Points.RemoveAt(polygon.Points.Count-2);_warning("多边形点数不超过 "+_maximumPoints);return;}
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
        if(!Alive){_cancel();return;}
        if(Substitute is not PolygonROISubstitute polygon)return;
        if(polygon.Points.Count<=2){_cancel();return;}
        polygon.Points.RemoveAt(polygon.Points.Count-2);
    }
    internal void Release()=>Guard(()=>ReleaseCore());
    private void ReleaseCore(){if(!Alive){_cancel();return;}if(!_moved&&!(Substitute is RectROISubstitute rect&&(rect.SubstituteRect.Width>0||rect.SubstituteRect.Height>0))
            &&!(Substitute is CircleROISubstitute circle&&circle.Radius>0)&&!(Substitute is SectorROISubstitute sector&&sector.Radius>0))
            {_cancel();return;}Finish();}
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
        if(_disposed)return;_disposed=true;
        NativeRoiCallbacks.Run(new Action[]{
            ()=>{_page.MouseMove-=OnMove;_page.MouseLeftButtonUp-=OnUp;_page.MouseLeftButtonDown-=OnDown;_page.MouseRightButtonUp-=OnRight;_page.LostMouseCapture-=OnLost;_page.Unloaded-=OnUnloaded;},
            ()=>{_template?.RemoveValueChanged(_page,OnTemplate);_template=null;},
            ()=>_host?.Children.Remove(Substitute),
            ()=>{if(ReferenceEquals(Mouse.Captured,_page))_page.ReleaseMouseCapture();}});
    }
}
