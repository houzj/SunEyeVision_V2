using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.UI.Services.PathCalculators.VmRouting.Original;
namespace SunEyeVision.UI.Services.PathCalculators.VmRouting;
public enum VmRouteStatus { Success, OriginalFallback, InvalidInput, BudgetExceeded }
public sealed record VmRouteResult(VmRouteStatus Status,IReadOnlyList<RoutePoint> Points);
/// <summary>Original VM 5-unit AStar and PathFinder pipeline. Original fallback can cross unrelated nodes.</summary>
public static class VmOrthogonalRouter
{
    private static readonly object Gate=new();
    // DiagramPage retains one APoint[,] map and resets its per-search state through
    // APoint.CurTimeEnableFlag. Keep the same lifecycle to avoid allocating a million
    // APoint objects for every connection on the fixed 5000x5000 workflow canvas.
    private static APoint[,]? cachedMap;
    private static int cachedMapWidth,cachedMapHeight;
    public static VmRouteResult Route(RoutePort source,RoutePort target,IEnumerable<RouteRect> obstacles,double canvasWidth,double canvasHeight,int maxCells=250000)
    {
        var raw=obstacles.Append(source.Owner).Append(target.Owner).Distinct().ToArray();
        if(!double.IsFinite(canvasWidth)||!double.IsFinite(canvasHeight)||canvasWidth<0||canvasHeight<0||canvasWidth>int.MaxValue-160||canvasHeight>int.MaxValue-160||
            !Valid(source)||!Valid(target)||raw.Any(r=>!double.IsFinite(r.X)||!double.IsFinite(r.Y)||!double.IsFinite(r.Right)||!double.IsFinite(r.Bottom)||r.X<0||r.Y<0||r.Width<=0||r.Height<=0))return Fail(VmRouteStatus.InvalidInput);
        int width=(int)canvasWidth/5+32,height=(int)canvasHeight/5+32;
        if((long)width*height>maxCells)return Fail(VmRouteStatus.BudgetExceeded);
        Point start=Offset(source),end=Offset(target);int sx=(int)start.X/5+1,sy=(int)start.Y/5+1,tx=(int)end.X/5+1,ty=(int)end.Y/5+1;
        if(sx<0||sy<0||tx<0||ty<0||sx>=width||tx>=width||sy>=height||ty>=height)return Fail(VmRouteStatus.InvalidInput);
        var all=raw.Select(Inflated).ToList();var pair=new List<Rect>{Inflated(source.Owner),Inflated(target.Owner)};
        lock(Gate)
        {
            if(cachedMap is null||cachedMapWidth!=width||cachedMapHeight!=height)
            {
                cachedMap=new APoint[width,height];cachedMapWidth=width;cachedMapHeight=height;
                for(int x=0;x<width;x++)for(int y=0;y<height;y++)cachedMap[x,y]=new APoint(x,y);
            }
            var map=cachedMap!;
            void Mark(List<Rect> walls)
            {
                APoint.CurTimeEnableFlag=Guid.NewGuid().ToString();
                foreach(var r in walls)for(int k=1;k<r.Width/5 && k+(int)r.X/5+1<width;k++)for(int l=1;l<r.Height/5 && l+(int)r.Y/5+1<height;l++)
                {int x=k+(int)r.X/5+1,y=l+(int)r.Y/5+1;if(x<width&&y<height)map[x,y].IsWall=true;}
            }
            Mark(all);var path=AStar.FindPath(new(sx,sy),new(tx,ty),map);bool fallback=path.Count<3;
            if(fallback){Mark(pair);all=pair;path=AStar.FindPath(new(sx,sy),new(tx,ty),map);}
            var points=new List<Point>();
            if(path.Count<3){if(start.X!=end.X&&start.Y!=end.Y)points.Add(new(start.X,end.Y));points.Insert(0,start);points.Add(end);}
            else foreach(var p in path)points.Add(new((p.X-1)*5,(p.Y-1)*5));
            points=PathFinder.OptimizeLinePoints(points,pair,all,Orientation(source.Direction),Orientation(target.Direction));
            points.Insert(0,new(source.Anchor.X,source.Anchor.Y));points.Add(new(target.Anchor.X,target.Anchor.Y));
            var turns=PathFinder.GetTurnPoints(points);PathFinder.RechargeLinePoints(turns,all);
            return new(fallback?VmRouteStatus.OriginalFallback:VmRouteStatus.Success,turns.Select(p=>new RoutePoint(p.X,p.Y)).ToArray());
        }
    }
    public static IReadOnlyList<RoutePoint> Preview(RoutePort source,RoutePoint sink)=>PathFinder.GetConnectionLine(source,new Point(sink.X,sink.Y)).Select(p=>new RoutePoint(p.X,p.Y)).ToArray();
    public static IReadOnlyList<RoutePoint> Passivate(IReadOnlyList<RoutePoint> points){var copy=points.Select(p=>new Point(p.X,p.Y)).ToList();PathFinder.Passivation(copy);return copy.Select(p=>new RoutePoint(p.X,p.Y)).ToArray();}
    private static VmRouteResult Fail(VmRouteStatus s)=>new(s,Array.Empty<RoutePoint>());
    private static Rect Inflated(RouteRect r)=>new(r.X-5,r.Y-5,r.Width+10,r.Height+10);
    private static ConnectorOrientation Orientation(PortDirection d)=>d switch{PortDirection.Left=>ConnectorOrientation.Left,PortDirection.Right=>ConnectorOrientation.Right,PortDirection.Up=>ConnectorOrientation.Top,_=>ConnectorOrientation.Bottom};
    private static Point Offset(RoutePort p)=>p.Direction switch{PortDirection.Left=>new(p.Anchor.X-5,p.Anchor.Y),PortDirection.Right=>new(p.Anchor.X+5,p.Anchor.Y),PortDirection.Up=>new(p.Anchor.X,p.Anchor.Y-5),_=>new(p.Anchor.X,p.Anchor.Y+5)};
    private static bool Valid(RoutePort p)=>double.IsFinite(p.Anchor.X)&&double.IsFinite(p.Anchor.Y)&&Enum.IsDefined(p.Direction)&&p.Direction switch {
        PortDirection.Left=>p.Anchor.X==p.Owner.X&&p.Anchor.Y>=p.Owner.Y&&p.Anchor.Y<=p.Owner.Bottom,
        PortDirection.Right=>p.Anchor.X==p.Owner.Right&&p.Anchor.Y>=p.Owner.Y&&p.Anchor.Y<=p.Owner.Bottom,
        PortDirection.Up=>p.Anchor.Y==p.Owner.Y&&p.Anchor.X>=p.Owner.X&&p.Anchor.X<=p.Owner.Right,
        _=>p.Anchor.Y==p.Owner.Bottom&&p.Anchor.X>=p.Owner.X&&p.Anchor.X<=p.Owner.Right};
}

