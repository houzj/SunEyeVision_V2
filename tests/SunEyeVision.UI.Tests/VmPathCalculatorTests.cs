using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Services.Path;
using SunEyeVision.UI.Services.PathCalculators;
using Xunit;
namespace SunEyeVision.UI.Tests;
public sealed class VmPathCalculatorTests
{
    public static IEnumerable<object[]> PortPairs()=>Enum.GetValues<PortDirection>().SelectMany(a=>Enum.GetValues<PortDirection>().Select(b=>new object[]{a,b}));
    private static Point Anchor(Rect rect,PortDirection side)=>side switch
    {PortDirection.Left=>new(rect.Left,rect.Top+rect.Height/2),PortDirection.Right=>new(rect.Right,rect.Top+rect.Height/2),PortDirection.Top=>new(rect.Left+rect.Width/2,rect.Top),_=>new(rect.Left+rect.Width/2,rect.Bottom)};
    [Theory]
    [MemberData(nameof(PortPairs))]
    public void All_sixteen_port_pairs_reach_their_actual_anchors(PortDirection from,PortDirection to)
    {
        var source=new Rect(300,100,110,30);var target=new Rect(100,300,110,30);
        var points=new VmPathCalculator().CalculateOrthogonalPath(Anchor(source,from),Anchor(target,to),from,to,source,target);
        Assert.Equal(Anchor(source,from),points[0]);Assert.Equal(Anchor(target,to),points[^1]);
        Assert.All(points,p=>Assert.True(double.IsFinite(p.X)&&double.IsFinite(p.Y)));
        Assert.DoesNotContain(points.Skip(1).SkipLast(1),p=>p.X>source.Left&&p.X<source.Right&&p.Y>source.Top&&p.Y<source.Bottom);
        Assert.DoesNotContain(points.Skip(1).SkipLast(1),p=>p.X>target.Left&&p.X<target.Right&&p.Y>target.Top&&p.Y<target.Bottom);
    }    [Fact]
    public void Route_reaches_actual_ports_and_creates_line_segments()
    {
        var calculator=new VmPathCalculator();
        var source=new Point(210,115);var target=new Point(400,215);
        var points=calculator.CalculateOrthogonalPath(source,target,PortDirection.Right,PortDirection.Left,
            new Rect(100,100,110,30),new Rect(400,200,110,30),new Rect(275,90,60,160));
        Assert.Equal(source,points[0]);Assert.Equal(target,points[^1]);
        var geometry=calculator.CreatePathGeometry(points);
        Assert.All(Assert.Single(geometry.Figures).Segments,s=>Assert.IsType<LineSegment>(s));
        Assert.Equal(points.Length-1,geometry.Figures[0].Segments.Count);
    }
    [Fact]
    public void Invalid_port_anchor_is_reported_instead_of_drawing_a_false_route()
    {
        var calculator=new VmPathCalculator();
        Assert.Throws<InvalidOperationException>(()=>calculator.CalculateOrthogonalPath(new Point(200,115),new Point(400,215),
            PortDirection.Right,PortDirection.Left,new Rect(100,100,110,30),new Rect(400,200,110,30)));
    }
    [Fact]
    public void Pointer_preview_preserves_mouse_endpoint()
    {
        var calculator=new VmPathCalculator();var pointer=new Point(363,287);
        var points=calculator.CalculatePreviewPath(new Point(210,115),pointer,PortDirection.Right,new Rect(100,100,110,30));
        Assert.Equal(new Point(210,115),points[0]);Assert.Equal(pointer,points[^1]);
    }
}