using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.UI.Services.PathCalculators.VmRouting.Original;
// System.Windows value-type geometry adapter. Closed/inclusive rectangle intersection mirrors WPF Rect.
internal readonly record struct Point(double X,double Y)
{
    public static Vector Subtract(Point a,Point b)=>new(a.X-b.X,a.Y-b.Y);
}
internal readonly record struct Vector(double X,double Y)
{
    public double Length=>Math.Sqrt(X*X+Y*Y);
    public static double CrossProduct(Vector a,Vector b)=>a.X*b.Y-a.Y*b.X;
}
internal struct Rect
{
    public double X,Y,Width,Height;
    public Rect(double x,double y,double width,double height){X=x;Y=y;Width=width;Height=height;}
    public Rect(Point a,Point b):this(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y)){}
    public Point TopLeft=>new(X,Y);public Point TopRight=>new(X+Width,Y);
    public Point BottomLeft=>new(X,Y+Height);public Point BottomRight=>new(X+Width,Y+Height);
    public void Inflate(double x,double y){X-=x;Y-=y;Width+=2*x;Height+=2*y;}
    public bool Contains(Point p)=>p.X>=X&&p.X<=X+Width&&p.Y>=Y&&p.Y<=Y+Height;
    public bool IntersectsWith(Rect r)=>Width>=0&&Height>=0&&r.Width>=0&&r.Height>=0&&r.X<=X+Width&&r.X+r.Width>=X&&r.Y<=Y+Height&&r.Y+r.Height>=Y;
}
