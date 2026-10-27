using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
namespace SunEyeVision.UI.Services.Rendering;
/// <summary>VM四向执行端口：独立热区与朝向内侧的三边矩形手柄。</summary>
public sealed class VmExecutionPort : Shape
{
    public static readonly DependencyProperty IsHighlightedProperty=DependencyProperty.Register(nameof(IsHighlighted),typeof(bool),typeof(VmExecutionPort),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsMeasure|FrameworkPropertyMetadataOptions.AffectsRender));
    public bool IsHighlighted {get=>(bool)GetValue(IsHighlightedProperty);set=>SetValue(IsHighlightedProperty,value);}
    public Point Anchor=>Tag as string switch
    {"LeftPort"=>new(Width,Height/2),"RightPort"=>new(0,Height/2),"TopPort"=>new(Width/2,Height),_=>new(Width/2,0)};
    protected override Geometry DefiningGeometry
    {
        get
        {
            bool hot=IsHighlighted||IsMouseOver;
            bool vertical=Tag is "LeftPort" or "RightPort";
            double w=vertical?(hot?8.5:5):(hot?12:8),h=vertical?(hot?12:8):(hot?8.5:5);
            var a=Anchor;double x=Tag as string switch{"LeftPort"=>a.X,"RightPort"=>a.X-w,_=>a.X-w/2};
            double y=Tag as string switch{"TopPort"=>a.Y,"BottomPort"=>a.Y-h,_=>a.Y-h/2};
            Point tl=new(x,y),tr=new(x+w,y),bl=new(x,y+h),br=new(x+w,y+h);
            Point[] outline=Tag as string switch{"LeftPort"=>new[]{tl,tr,br,bl},"RightPort"=>new[]{tr,tl,bl,br},"TopPort"=>new[]{tl,bl,br,tr},_=>new[]{bl,tl,tr,br}};
            var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(outline[0],true,false);context.PolyLineTo(outline.Skip(1).ToArray(),true,false);}geometry.Freeze();return geometry;
        }
    }
    protected override Size MeasureOverride(Size availableSize)=>new(Width,Height);
    protected override Size ArrangeOverride(Size finalSize)=>finalSize;
    protected override void OnRender(DrawingContext context)
    {
        context.DrawGeometry(Fill,new Pen(Stroke,IsHighlighted||IsMouseOver?2:1),DefiningGeometry);
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters parameters)=>new Rect(0,0,ActualWidth,ActualHeight).Contains(parameters.HitPoint)?new PointHitTestResult(this,parameters.HitPoint):null;
    protected override void OnMouseEnter(MouseEventArgs e){base.OnMouseEnter(e);InvalidateMeasure();InvalidateVisual();}
    protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);InvalidateMeasure();InvalidateVisual();}
}