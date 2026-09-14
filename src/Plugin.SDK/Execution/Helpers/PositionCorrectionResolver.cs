using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers;

/// <summary>
/// 位置修正矩阵解析。
/// ① 输入解析：多个矩阵自动只用第一个（ShapeMatch 契约：最佳=[0]，方向=参考图像→测试图像）；
/// ② 形状语义：无向形状"中心完整修正 + 朝向不旋转"，有向形状完整跟随（全仓库唯一语义源）。
/// </summary>
public static class PositionCorrectionResolver
{
    /// <summary>
    /// 从绑定源解析首选位置修正矩阵。
    /// 单值 <see cref="AffineMatrix"/> 直接用；数组输入取 [0]
    /// （多目标多次检测请通过子程序循环逐实例执行，不在位置修正绑定中展开）。
    /// </summary>
    /// <param name="bindingSource">位置修正绑定源（可为 null）</param>
    /// <returns>首选矩阵；无有效矩阵返回 null</returns>
    public static AffineMatrix? ResolveFirstMatrix(AvailableDataSource? bindingSource)
    {
        var current = bindingSource?.CurrentValue;
        return current switch
        {
            AffineMatrix matrix => matrix,
            AffineMatrix[] matrices when matrices.Length > 0 => matrices[0],
            _ => null,
        };
    }

    /// <summary>
    /// 无向形状判定：轴对齐矩形 / 圆 / 圆环没有方向语义。
    /// 其余几何（旋转矩形 / 多边形 / 直线 / 未知类型）按有向处理。
    /// </summary>
    public static bool IsDirectionless(object? geometry) => geometry switch
    {
        // P0-2 封口（完全删除 WPF Rect）：矩形几何恒为 RotatedRect2d —— 具备方向语义（轴对齐即 Angle=0）。
        Circle2d or Annulus2d => true,
        _ => false,
    };

    /// <summary>
    /// 按形状有向性选择位置修正矩阵（全仓库唯一语义源，禁止各层自行解释矩阵）。
    ///   无向形状（轴对齐矩形/圆/圆环）→ "中心完整修正 + 朝向不旋转"矩阵：
    ///      中心经完整矩阵变换（含旋转造成的位移，真正修正到目标位置），
    ///      相对中心的偏移只做等比缩放、不旋转（图形保持水平 / 手柄保持四向）；
    ///   有向形状 / 未知几何 → 完整矩阵（跟随旋转）。
    /// 矩阵为 null 时原样返回 null。
    /// </summary>
    public static AffineMatrix? ResolveForShape(object? geometry, AffineMatrix? fullMatrix)
    {
        if (fullMatrix == null) return null;

        return geometry switch
        {
            Circle2d c => CenterPreservingStrip(fullMatrix, new System.Windows.Point(c.Center.X, c.Center.Y)),
            Annulus2d a => CenterPreservingStrip(fullMatrix, new System.Windows.Point(a.Center.X, a.Center.Y)),
            // P0-2 封口：不再有 WPF Rect 分支（矩形几何恒为 RotatedRect2d）。
            _ => fullMatrix,
        };
    }

    /// <summary>
    /// 构造无向形状的位置修正矩阵 M'：M'(p) = s·p + (M(c) − s·c)。
    /// 其中 c = 参考系形状中心，s = 均匀缩放，M = 完整矩阵。
    /// 性质：
    ///   - 形状中心被完整修正：M'(c) = M(c)（含旋转分量造成的位移，中心定位准确）；
    ///   - 相对中心的偏移只缩放不旋转：M'(c+o) − M'(c) = s·o（图形不转、手柄保持轴向）。
    /// </summary>
    private static AffineMatrix CenterPreservingStrip(AffineMatrix full, System.Windows.Point refCenter)
    {
        double s = full.ScaleFactor();
        var displayCenter = full.TransformWpfPoint(refCenter);
        return new AffineMatrix(
            s, 0, 0, s,
            displayCenter.X - s * refCenter.X,
            displayCenter.Y - s * refCenter.Y);
    }

    /// <summary>
    /// 参考系角度 → 显示系角度：把参考方向向量（角度 <paramref name="refAngleDeg"/>）
    /// 经矩阵 <paramref name="matrix"/> 变换后反推屏幕角，供有向形状叠加位置修正旋转。
    /// 直接由矩阵作用于方向向量计算，避免手工约定矩阵旋转符号造成错位。
    /// </summary>
    public static double DisplayAngleDeg(AffineMatrix matrix, double refAngleDeg)
    {
        double a = refAngleDeg * Math.PI / 180.0;
        double vx = Math.Cos(a);
        double vy = Math.Sin(a);
        double dx = vx * matrix.M11 + vy * matrix.M21;
        double dy = vx * matrix.M12 + vy * matrix.M22;
        return Math.Atan2(dy, dx) * 180.0 / Math.PI;
    }
}
