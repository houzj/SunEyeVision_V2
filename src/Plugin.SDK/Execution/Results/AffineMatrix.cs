using System;
using System.Text.Json.Serialization;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>
/// Affine matrix for position correction binding.
/// 6 elements: [M11, M12, M21, M22, Tx, Ty]
/// Aligned with Halcon hom_mat2d semantics: P' = M \u00b7 P
/// Rendering and position correction share the same matrix with same direction (model to image).
/// </summary>
public class AffineMatrix
{
    public double M11 { get; set; } = 1;
    public double M12 { get; set; }
    public double M21 { get; set; }
    public double M22 { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }

    public AffineMatrix() { }

    [JsonConstructor]
    public AffineMatrix(double m11, double m12, double m21, double m22,
        double offsetX, double offsetY)
    {
        M11 = m11; M12 = m12; M21 = m21; M22 = m22;
        OffsetX = offsetX; OffsetY = offsetY;
    }

    /// <summary>Convert to WPF Matrix (downstream tools use this directly in forward direction)</summary>
    public System.Windows.Media.Matrix ToWpfMatrix() =>
        new(M11, M12, M21, M22, OffsetX, OffsetY);

    /// <summary>Construct from WPF Matrix</summary>
    public static AffineMatrix FromWpfMatrix(System.Windows.Media.Matrix m) =>
        new(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY);

    /// <summary>
    /// 矩阵缩放因子（列向量模长，几何半径/尺寸随矩阵缩放的比例）
    /// </summary>
    public double ScaleFactor() => Math.Sqrt(M11 * M11 + M21 * M21);

    /// <summary>Construct from center, angle (radians), and scale</summary>
    public static AffineMatrix FromCenterAngleScale(
        double centerX, double centerY, double angleRad, double scale)
    {
        double cos = Math.Cos(angleRad);
        double sin = Math.Sin(angleRad);
        return new AffineMatrix(
            scale * cos,  scale * sin,
           -scale * sin,  scale * cos,
            centerX,      centerY);
    }

    public static AffineMatrix Identity => new(1, 0, 0, 1, 0, 0);

    public override string ToString() =>
        $"[{M11:F4},{M12:F4},{M21:F4},{M22:F4},{OffsetX:F4},{OffsetY:F4}]";

    public double[] ToArray() => [M11, M12, M21, M22, OffsetX, OffsetY];

    /// <summary>Transform a point from reference coordinates to test coordinates (forward).</summary>
    /// <remarks>
    /// 与 ToWpfMatrix() / ToHalconArray() 对齐，使用标准行向量乘法：
    /// P'x = Px*M11 + Py*M21 + Ox
    /// P'y = Px*M12 + Py*M22 + Oy
    /// </remarks>
    public (double X, double Y) TransformPoint(double x, double y)
    {
        var newX = x * M11 + y * M21 + OffsetX;
        var newY = x * M12 + y * M22 + OffsetY;
        return (newX, newY);
    }

    /// <summary>Transform a WPF point from reference coordinates to test coordinates (forward).</summary>
    public Point TransformWpfPoint(Point p)
    {
        var (x, y) = TransformPoint(p.X, p.Y);
        return new Point(x, y);
    }

    /// <summary>Compute inverse matrix (test → reference).</summary>
    public AffineMatrix Inverse()
    {
        double det = M11 * M22 - M12 * M21;
        if (Math.Abs(det) < 1e-10)
            throw new InvalidOperationException("位置修正矩阵不可逆");
        double invDet = 1.0 / det;
        return new AffineMatrix(
            M22 * invDet, -M12 * invDet,
            -M21 * invDet,  M11 * invDet,
            (M21 * OffsetY - M22 * OffsetX) * invDet,
            (M12 * OffsetX - M11 * OffsetY) * invDet);
    }

    /// <summary>
    /// 转为 Halcon hom_mat2D 数组顺序
    /// Halcon affine_trans_region: r' = H[0]*r + H[1]*c + H[2]
    ///                              c' = H[3]*r + H[4]*c + H[5]
    /// WPF Matrix (行=Y, 列=X): P'x = Px*M11 + Py*M21 + Ox
    ///                          P'y = Px*M12 + Py*M22 + Oy
    /// => H[0]=M22, H[1]=M12, H[2]=OffsetY
    ///    H[3]=M21, H[4]=M11, H[5]=OffsetX
    /// </summary>
    public double[] ToHalconArray() => [M22, M12, OffsetY, M21, M11, OffsetX];
}
