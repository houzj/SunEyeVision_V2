namespace SunEyeVision.UI.Services.PathCalculators.VmRouting;

public readonly record struct RoutePoint(double X, double Y);
public readonly record struct RouteRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public RouteRect Inflate(double margin) => new(X-margin,Y-margin,Width+2*margin,Height+2*margin);
}
public enum PortDirection { Left, Right, Up, Down }
public readonly record struct RoutePort(RoutePoint Anchor, PortDirection Direction, RouteRect Owner);