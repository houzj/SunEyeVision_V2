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
/// <remarks>
/// 坐标系约定（框架唯一事实源，两端只读不产生）：
///   本矩阵定义于 WPF 屏幕坐标系（X 向右、Y 向下），行向量乘法：
///     P'x = Px*M11 + Py*M21 + OffsetX
///     P'y = Px*M12 + Py*M22 + OffsetY
///   θ 正方向 = 视觉顺时针（WPF Matrix 惯例，y 向下）。
///   矩阵数值由算法权威侧（如各工具工厂）构造，框架内只流通本类型；
///   Halcon 边界经 ToHalconArray() 适配，WPF 边界经 ToWpfMatrix() 适配，
///   禁止在类型外自行补符号或改坐标系（对齐 Geometry 层 HalconArcAdapter 范式）。
/// </remarks>
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
    /// <remarks>
    /// 对齐 Halcon `vector_angle_to_rigid(0,0,0,row,col,angle)`:
    ///   iRow = cos·r − sin·c + row
    ///   iCol = sin·r + cos·c + col
    ///   即 (M11, M12, M21, M22) = (cos, −sin, sin, cos)，与 WPF Matrix.Rotate 一致（视觉顺时针 θ）。
    ///   历史 bug：原 M12=+sin / M21=−sin 与 Halcon 不兼容，致轮廓错位（AC-2 方向契约首版测试断言写反，
    ///   误把 bug 固化为期望值，方向契约测试因此"通过"但实际渲染偏移）。
    /// </remarks>
    public static AffineMatrix FromCenterAngleScale(
        double centerX, double centerY, double angleRad, double scale)
    {
        double cos = Math.Cos(angleRad);
        double sin = Math.Sin(angleRad);
        return new AffineMatrix(
            scale * cos,  -scale * sin,
            scale * sin,   scale * cos,
            centerX,       centerY);
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
