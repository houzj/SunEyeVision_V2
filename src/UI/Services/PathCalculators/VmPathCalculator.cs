using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Services.Path;
using Vm = SunEyeVision.UI.Services.PathCalculators.VmRouting;
using Direction = SunEyeVision.UI.Services.Path.PortDirection;
namespace SunEyeVision.UI.Services.PathCalculators;

/// <summary>将宿主中心坐标与四向端口接入恢复的VM正交路由，不改变工作流模型。</summary>
public sealed class VmPathCalculator : IPathCalculator
{
    private static Vm.PortDirection Convert(Direction d)=>d switch
    {Direction.Left=>Vm.PortDirection.Left,Direction.Right=>Vm.PortDirection.Right,Direction.Top=>Vm.PortDirection.Up,_=>Vm.PortDirection.Down};
    private static Vm.RouteRect Convert(Rect r)=>new(r.X,r.Y,r.Width,r.Height);
    private static Rect Owner(Point p,Direction d)=>d switch
    {Direction.Left=>new(p.X,p.Y-15,110,30),Direction.Right=>new(p.X-110,p.Y-15,110,30),Direction.Top=>new(p.X-55,p.Y,110,30),_=>new(p.X-55,p.Y-30,110,30)};
    public Point[] CalculateOrthogonalPath(Point source,Point target,Direction from,Direction to)
    {
        var rect=Owner(source,from);
        return Vm.VmOrthogonalRouter.Preview(new(new(source.X,source.Y),Convert(from),Convert(rect)),new(target.X,target.Y))
            .Select(p=>new Point(p.X,p.Y)).ToArray();
    }
    public Point[] CalculateOrthogonalPath(Point source,Point target,Direction from,Direction to,Rect sourceRect,Rect targetRect,params Rect[] obstacles)
    {
        var result=Vm.VmOrthogonalRouter.Route(new(new(source.X,source.Y),Convert(from),Convert(sourceRect)),
            new(new(target.X,target.Y),Convert(to),Convert(targetRect)),obstacles.Select(Convert),5000,5000,1_100_000);
        if(result.Status is Vm.VmRouteStatus.InvalidInput or Vm.VmRouteStatus.BudgetExceeded)
            throw new InvalidOperationException("VM连线路由输入超出画布或端口边界："+result.Status);
        return result.Points.Select(p=>new Point(p.X,p.Y)).ToArray();
    }
    public PathGeometry CreatePathGeometry(Point[] points)
    {
        var geometry=new PathGeometry();
        if(points.Length<2)return geometry;
        var figure=new PathFigure{StartPoint=points[0],IsClosed=false,IsFilled=false};
        foreach(var p in points.Skip(1))figure.Segments.Add(new LineSegment(p,true));
        geometry.Figures.Add(figure);return geometry;
    }
    public (Point position,double angle) CalculateArrow(Point[] points,Point target,Direction direction)
    {
        var prior=Enumerable.Reverse(points).FirstOrDefault(p=>p!=target);
        return (target,Math.Atan2(target.Y-prior.Y,target.X-prior.X)*180/Math.PI);
    }
}