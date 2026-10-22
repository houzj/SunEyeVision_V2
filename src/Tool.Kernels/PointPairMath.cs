namespace SunEyeVision.Tool.Kernels;
/// <summary>Shared numeric kernel extracted from PointMeasurementGeometry.MeasurePair without changing its formula.</summary>
public static class PointPairMath
{
    public static (double Distance, double DeltaX, double DeltaY, double AngleDeg) Measure(double x1, double y1, double x2, double y2)
    {
        static double Finite(double v, string name) => double.IsFinite(v) ? v : throw new InvalidOperationException($"{name} 不是有限数值（NaN／Infinity或计算溢出），本次测量失败。");
        Finite(x1, "点1.X"); Finite(y1, "点1.Y"); Finite(x2, "点2.X"); Finite(y2, "点2.Y");
        var dx = Finite(x2-x1, "ΔX"); var dy = Finite(y2-y1, "ΔY");
        return (Finite(double.Hypot(dx,dy), "点距"), dx, dy, Finite(Math.Atan2(dy,dx)*180.0/Math.PI, "连线角度"));
    }
}
