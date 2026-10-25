using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.Wpf.Regions;
using WpfCanvas = System.Windows.Controls.Canvas;

namespace SunEyeVision.UI.Services.Editing.Regions;

/// <summary>One host-owned interaction lease and one child layer. Never owns or clears the shared result layer.</summary>
public sealed class RoiViewportEditScope : IRoiEditViewport,IDisposable
{
    private sealed class Slot {public RoiViewportEditScope? Active;}
    private static readonly ConditionalWeakTable<ImageControl,Slot> Slots=new();
    private readonly ImageControl _image;
    private readonly Func<bool> _isCurrent;
    private readonly Slot _slot;
    private readonly RoiLayer _layer;
    private readonly WpfCanvas _surface;
    private readonly MouseButtonEventHandler _down,_up;
    private readonly MouseEventHandler _move;
    private IReadOnlyList<RoiEditItem> _items=Array.Empty<RoiEditItem>();
    private string _group="inspection";
    private Guid? _selected;
    private string? _drawing;
    private Point2? _anchor;
    private readonly List<Point2> _points=new();
    private RecordValue? _original,_candidate;
    private Matrix _pose=Matrix.Identity;
    private object? _gestureImage;
    private Guid? _gestureFrame;
    private bool _disposed;
    private bool _poseReady=true;
    private string _poseError="";
    private RoiEditAction _action;
    private HashSet<string> _editableGroups=new(StringComparer.Ordinal){"inspection","mask"};
    public bool IsActive=>!_disposed&&ReferenceEquals(_slot.Active,this);
    public bool HasPendingGesture=>_anchor.HasValue||_points.Count>0;
    public event Action<RoiGeometryChange>? GeometryChanged;
    public event Action<string,Guid?>? SelectionChanged;
    public event Action<string>? StatusChanged;

    public RoiViewportEditScope(ImageControl image,Func<bool> isCurrent)
    {
        _image=image;_isCurrent=isCurrent;_slot=Slots.GetValue(image,_=>new());
        _surface=(WpfCanvas)(image.FindName("MainCanvas")??throw new InvalidOperationException("图像视口未就绪。"));
        _layer=new(this){IsHitTestVisible=false,Visibility=Visibility.Hidden};image.InteractionOverlayCanvasControl.Children.Add(_layer);
        _down=(_,e)=>{if(IsActive&&_poseReady&&OnSurface(e.OriginalSource)&&PointerDown(ToReference(e),e.ClickCount)){if(HasPendingGesture)_surface.CaptureMouse();e.Handled=true;}};
        _move=(_,e)=>{if(IsActive&&_poseReady&&OnSurface(e.OriginalSource)&&PointerMove(ToReference(e)))e.Handled=true;};
        _up=(_,e)=>{if(IsActive&&_poseReady&&PointerUp(ToReference(e))){if(ReferenceEquals(Mouse.Captured,_surface))_surface.ReleaseMouseCapture();e.Handled=true;}};
        _image.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,_down,true);
        _image.AddHandler(UIElement.PreviewMouseMoveEvent,_move,true);
        _image.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,_up,true);
        _image.ViewTransformed+=OnView;_image.ImageLoaded+=OnImage;
        _image.PreviewKeyDown+=OnKey;
        Redraw();
    }
    public void Activate()
    {
        EnsureCurrent();if(ReferenceEquals(_slot.Active,this))return;
        _slot.Active?.Suspend();_slot.Active=this;_layer.Visibility=Visibility.Visible;Redraw();
    }
    private void Suspend(){CancelGesture();_layer.Visibility=Visibility.Hidden;}
    public void SetScene(IEnumerable<RoiEditItem> items)
    {_image.Dispatcher.VerifyAccess();if(HasPendingGesture){CancelGesture();StatusChanged?.Invoke("区域配置已变化，本次绘制已取消。");}_items=items.ToArray();if(_selected.HasValue&&!_items.Any(item=>item.Group==_group&&item.Id==_selected))_selected=null;Redraw();}
    public void SetEditableGroups(IEnumerable<string> groups)
    {_editableGroups=groups.ToHashSet(StringComparer.Ordinal);if(!_editableGroups.Contains(_group))CancelGesture();}
    public void SetPose(Matrix? pose,bool ready,string error="")
    {
        var next=pose??Matrix.Identity;if(_pose!=next||_poseReady!=ready)CancelGesture();
        _pose=next;_poseReady=ready&&_pose.HasInverse&&new[]{next.M11,next.M12,next.M21,next.M22,next.OffsetX,next.OffsetY}.All(double.IsFinite)
            &&Math.Abs(next.M11*next.M22-next.M12*next.M21)>1e-10;_poseError=error;Redraw();
    }
    public bool BeginDraw(string group,string kind,out string error)
    {
        error="";try
        {
            Activate();CheckDrawable();if(!_editableGroups.Contains(group))throw new InvalidOperationException("当前区域采用继承来源，不能写入本地几何。");if(!RoiGeometryEditing.Kinds.Contains(kind))throw new ArgumentException("未知区域形状。");
            CancelGesture();_group=group;_drawing=kind;_selected=null;SelectionChanged?.Invoke(group,null);
            StatusChanged?.Invoke(kind is "arc" or "annulus"?"依次点击圆心、外缘和结束点；Esc取消。":kind=="polygon"?"依次点击顶点，双击或Enter完成；Esc取消。":"在图像上绘制；Esc取消。");return true;
        }
        catch(InvalidOperationException exception){error=exception.Message;return false;}
    }
    public void Select(string group,Guid? id){Activate();CancelGesture();_group=group;_selected=id;SelectionChanged?.Invoke(group,id);Redraw();}
    public void SetAction(RoiEditAction action){Activate();CancelGesture();_drawing=null;_action=action;}
    public void CancelGesture()
    {
        _drawing=null;_anchor=null;_original=null;_candidate=null;_points.Clear();_gestureImage=null;_gestureFrame=null;
        if(ReferenceEquals(Mouse.Captured,_surface)&&IsActive)_surface.ReleaseMouseCapture();Redraw();
    }
    public bool PointerDown(Point2 point,int clickCount=1)
    {
        if(!IsActive)return false;
        try
        {
            CheckDrawable();if(!HasPendingGesture){_gestureImage=_image.SourceImage;_gestureFrame=PreviewFrameProvenance.Read(_image.SourceImage);}
            if(_drawing is { } kind)
            {
                if(kind=="point"){Commit(null,RoiGeometryEditing.Create(kind,point,point));return true;}
                if(kind is "polygon" or "arc" or "annulus")
                {
                    if(clickCount>1&&kind=="polygon"){FinishPolygon();return true;}
                    if(_points.Count==0||_points[^1]!=point)_points.Add(point);
                    if(kind!="polygon"&&_points.Count==3)
                    {
                        var values=RoiGeometryEditing.Values(RoiGeometryEditing.Create(kind,_points[0],_points[1]));
                        var center=_points[0];var angle=Math.Atan2(_points[1].Y-center.Y,_points[1].X-center.X);
                        if(kind=="arc")
                        {var end=Math.Atan2(point.Y-center.Y,point.X-center.X);if(end<=angle)end+=2*Math.PI;values["start-radians"]=angle;values["end-radians"]=end;values["major-arc"]=end-angle>Math.PI;}
                        else values["inner-radius"]=Math.Sqrt(Math.Pow(point.X-center.X,2)+Math.Pow(point.Y-center.Y,2));
                        Commit(null,RoiGeometryEditing.Checked(values));
                    }
                    Redraw();return true;
                }
                _anchor=point;return true;
            }
            var hit=_action is RoiEditAction.Resize or RoiEditAction.Rotate?_items.SingleOrDefault(item=>item.Group==_group&&item.Id==_selected)??Hit(point):Hit(point);
            _selected=hit?.Id;_group=hit?.Group??_group;SelectionChanged?.Invoke(_group,_selected);
            if(hit is null){Redraw();return false;}
            _original=hit.Geometry;_anchor=point;_candidate=hit.Geometry;Redraw();return true;
        }
        catch(Exception exception) when(exception is ArgumentException or InvalidOperationException)
        {if(_drawing is "arc" or "annulus"&&_points.Count>2)_points.RemoveAt(_points.Count-1);StatusChanged?.Invoke(exception.Message);return true;}
    }
    public bool PointerMove(Point2 point)
    {
        if(!IsActive)return false;
        if(!_isCurrent()){CancelGesture();return false;}
        if(_gestureImage is not null&&(!ReferenceEquals(_gestureImage,_image.SourceImage)||_gestureFrame!=PreviewFrameProvenance.Read(_image.SourceImage))){CancelGesture();return false;}
        try
        {
            if(_anchor is { } anchor)_candidate=_drawing is { } kind?RoiGeometryEditing.Create(kind,anchor,point)
                :_original is { } original?RoiGeometryEditing.Manipulate(original,_action,anchor,point):null;
            else if(_drawing is { } multi&&_points.Count>0)
                _candidate=multi=="polygon"?_points.Count>=2?RoiGeometryEditing.Create(multi,_points[0],point,_points.Append(point).ToArray()):null
                    :RoiGeometryEditing.Create(multi,_points[0],_points.Count>1?_points[1]:point);
            else return false;
            Redraw();return true;
        }
        catch(ArgumentException){_candidate=null;Redraw();return true;}
    }
    public bool PointerUp(Point2 point)
    {
        if(!IsActive||!_anchor.HasValue)return false;if(!_isCurrent()){CancelGesture();return false;}PointerMove(point);
        if(_candidate is { } shape)Commit(_drawing is null?_selected:null,shape);else CancelGesture();return true;
    }
    public void FinishPolygon()
    {
        if(_drawing!="polygon"||_points.Count<3){StatusChanged?.Invoke("多边形至少需要三个顶点。");return;}
        Commit(null,RoiGeometryEditing.Create("polygon",_points[0],_points[^1],_points));
    }
    private void Commit(Guid? id,RecordValue geometry)
    {
        RoiGeometryEditing.Validate(geometry);var group=_group;_drawing=null;CancelGesture();
        GeometryChanged?.Invoke(new(group,id,geometry));StatusChanged?.Invoke("区域已写入当前草稿，确定后生效。");
    }
    private RoiEditItem? Hit(Point2 point)
    {
        var tolerance=6/Math.Max(.001,_image.Zoom);
        return _items.Reverse().Where(item=>item.Enabled&&item.Visible).FirstOrDefault(item=>
        {
            try
            {
                var center=RoiGeometryEditing.Center(item.Geometry);if(Math.Abs(point.X-center.X)<tolerance&&Math.Abs(point.Y-center.Y)<tolerance)return true;
                foreach(var path in RoiGeometryEditing.Outlines(item.Geometry))
                {
                    var points=path.Points;
                    for(var i=1;i<points.Length;i++)if(Distance(point,points[i-1],points[i])<=tolerance)return true;
                    if(path.Closed&&Inside(point,points))return true;
                }
            }catch(Exception exception) when(exception is ArgumentException or KeyNotFoundException){ }
            return false;
        });
    }
    private static double Distance(Point2 p,Point2 a,Point2 b)
    {var dx=b.X-a.X;var dy=b.Y-a.Y;var length=dx*dx+dy*dy;var t=length==0?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length,0,1);return Math.Sqrt(Math.Pow(p.X-a.X-t*dx,2)+Math.Pow(p.Y-a.Y-t*dy,2));}
    private static bool Inside(Point2 p,Point2[] points)
    {var inside=false;for(int i=0,j=points.Length-1;i<points.Length;j=i++)if((points[i].Y>p.Y)!=(points[j].Y>p.Y)&&p.X<(points[j].X-points[i].X)*(p.Y-points[i].Y)/(points[j].Y-points[i].Y)+points[i].X)inside=!inside;return inside;}
    private bool OnSurface(object source)=>source is DependencyObject item&&(ReferenceEquals(item,_surface)||IsDescendant(item,_surface));
    private static bool IsDescendant(DependencyObject item,DependencyObject ancestor)
    {for(var current=item;current is not null;current=current is Visual?VisualTreeHelper.GetParent(current):null)if(ReferenceEquals(current,ancestor))return true;return false;}
    private Point2 ToReference(MouseEventArgs args)
    {var point=args.GetPosition(_image.InteractionOverlayCanvasControl);return ImageToReference(new(point.X,point.Y));}
    public Point2 ImageToReference(Point2 point)
    {if(!_poseReady)throw new InvalidOperationException("位置修正未就绪。");var inverse=_pose;inverse.Invert();var result=inverse.Transform(new Point(point.X,point.Y));return new(result.X,result.Y);}
    private void CheckDrawable()
    {EnsureCurrent();if(_image.SourceImage is null)throw new InvalidOperationException("请先在图像视口加载或选择输入图像。");if(!_poseReady)throw new InvalidOperationException(_poseError.Length==0?"位置修正与当前预览未确认同帧，不能绘制。":_poseError);}
    private void EnsureCurrent(){_image.Dispatcher.VerifyAccess();if(_disposed||!_isCurrent())throw new InvalidOperationException("当前区域编辑范围已失效。");}
    private void OnView(object? sender,ViewTransformEventArgs args)=>Redraw();
    private void OnImage(object? sender,ImageLoadedEventArgs args){CancelGesture();Redraw();}
    private void OnKey(object sender,KeyEventArgs args)
    {if(!IsActive)return;if(args.Key==Key.Escape){_drawing=null;CancelGesture();args.Handled=true;}else if(args.Key==Key.Enter&&_drawing=="polygon"){FinishPolygon();args.Handled=true;}}
    private void Redraw(){_layer.Width=_image.SourceImage?.PixelWidth??0;_layer.Height=_image.SourceImage?.PixelHeight??0;_layer.InvalidateVisual();}
    public void Dispose()
    {
        if(_disposed)return;CancelGesture();_disposed=true;
        _image.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent,_down);_image.RemoveHandler(UIElement.PreviewMouseMoveEvent,_move);_image.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent,_up);
        _image.ViewTransformed-=OnView;_image.ImageLoaded-=OnImage;_image.PreviewKeyDown-=OnKey;
        _image.InteractionOverlayCanvasControl.Children.Remove(_layer);if(ReferenceEquals(_slot.Active,this))_slot.Active=null;
        GeometryChanged=null;SelectionChanged=null;StatusChanged=null;_items=Array.Empty<RoiEditItem>();
    }
    private sealed class RoiLayer(RoiViewportEditScope owner):FrameworkElement
    {
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);if(!owner._poseReady)return;
            context.PushTransform(new MatrixTransform(owner._pose));
            foreach(var item in owner._items.Where(item=>item.Enabled&&item.Visible))
                Draw(context,item.Id==owner._selected&&owner._candidate is not null?owner._candidate:item.Geometry,item.Group=="mask"?Brushes.Orange:Brushes.DeepSkyBlue,item.Id==owner._selected);
            if(owner._candidate is not null&&owner._drawing is not null)Draw(context,owner._candidate,Brushes.LimeGreen,true);
            context.Pop();
        }
        private void Draw(DrawingContext context,RecordValue shape,Brush color,bool selected)
        {
            try
            {
                var pen=new Pen(selected?Brushes.LimeGreen:color,(selected?2:1.5)/Math.Max(.001,owner._image.Zoom));
                foreach(var path in RoiGeometryEditing.Outlines(shape,Math.Max(Width,Height)*2))
                {
                    if(path.Points.Length<2)continue;var geometry=new StreamGeometry();
                    using(var stream=geometry.Open()){stream.BeginFigure(new(path.Points[0].X,path.Points[0].Y),false,path.Closed);stream.PolyLineTo(path.Points.Skip(1).Select(p=>new Point(p.X,p.Y)).ToArray(),true,false);}
                    context.DrawGeometry(null,pen,geometry);
                }
                if(selected){var center=RoiGeometryEditing.Center(shape);context.DrawEllipse(Brushes.White,pen,new(center.X,center.Y),3/owner._image.Zoom,3/owner._image.Zoom);}
            }catch(Exception exception) when(exception is ArgumentException or KeyNotFoundException){ }
        }
    }
}
