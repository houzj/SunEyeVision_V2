using System.Windows;
using System.Windows.Media;
using SunEyeVision.UI.Services.Path;
using Routing = SunEyeVision.UI.Services.PathCalculators.OrthogonalRouting;
using Direction = SunEyeVision.UI.Services.Path.PortDirection;
namespace SunEyeVision.UI.Services.PathCalculators;

/// <summary>将宿主中心坐标与四向端口接入恢复的VM正交路由，不改变工作流模型。</summary>
public sealed class OrthogonalPathCalculator : IPathCalculator
{
    private static Routing.PortDirection Convert(Direction d)=>d switch
    {Direction.Left=>Routing.PortDirection.Left,Direction.Right=>Routing.PortDirection.Right,Direction.Top=>Routing.PortDirection.Up,_=>Routing.PortDirection.Down};
    private static Routing.RouteRect Convert(Rect r)=>new(r.X,r.Y,r.Width,r.Height);
    public Point[] CalculatePreviewPath(Point source,Point target,Direction from,Rect sourceRect)
    {
        return Routing.OrthogonalRouter.Preview(new(new(source.X,source.Y),Convert(from),Convert(sourceRect)),new(target.X,target.Y))
            .Select(p=>new Point(p.X,p.Y)).ToArray();
    }
    public Point[] CalculateOrthogonalPath(Point source,Point target,Direction from,Direction to,Rect sourceRect,Rect targetRect,params Rect[] obstacles)
    {
        var result=Routing.OrthogonalRouter.Route(new(new(source.X,source.Y),Convert(from),Convert(sourceRect)),
            new(new(target.X,target.Y),Convert(to),Convert(targetRect)),obstacles.Select(Convert),5000,5000,1_100_000);
        if(result.Status is Routing.OrthogonalRouteStatus.InvalidInput or Routing.OrthogonalRouteStatus.BudgetExceeded)
            throw new InvalidOperationException("画布连线路由输入超出画布或端口边界："+result.Status);
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