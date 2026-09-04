using System;
using System.Linq;
using System.Windows;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.HalAdapter;

/// <summary>
/// 算法区域(IVisionRegion, iconic 运行时数据) 与 用户几何(RoI, ShapeParameters) 的双向桥接。
///
/// 设计原则（方案 D8）：工作流契约只认 RoI；Region 仅活在算法工具内部。
/// 工具在输出映射处调用 <see cref="RegionToRoi"/> 把 Region 转 RoI 外发，下游统一消费 RoI。
///
/// 失真边界（必须知情）：
/// 1. 孔洞丢失：<see cref="RegionToRoi"/> 默认 exact 用 GetRegionContour 仅取外轮廓，
///    Polygon2d 单环不支持孔，带孔区域(环/带孔 blob)的孔会丢失。Blob 流程通常先 FillUp，少见。
/// 2. 亚像素 1px 边界误差：轮廓为整数像素坐标，重栅格化可能差 ≤1px，对几何用途可忽略。
/// 3. fit-circle/fit-rect/fit-bbox 为显式 opt-in 的几何拟合，会产生失真，
///    须由调用方先用 <see cref="RegionCircularity"/> 判断（失真由用户知情选择，不静默）。
/// </summary>
public static class RegionConverter
{
    /// <summary>
    /// 将算法区域转换为 RoI。
    /// </summary>
    /// <param name="region">算法区域（iconic）</param>
    /// <param name="fitMode">exact(默认, 精确多边形零失真) / fit-circle / fit-rect / fit-bbox</param>
    /// <returns>用户几何 RoI</returns>
    public static ShapeParameters RegionToRoi(IVisionRegion region, string fitMode = "exact")
    {
        if (region == null) throw new ArgumentNullException(nameof(region));
        if (region.IsEmpty)
            throw new ArgumentException("区域为空，无法转换为 RoI", nameof(region));

        var contour = region.GetContour();
        if (contour.Length == 0)
            throw new ArgumentException("区域无轮廓数据，无法转换为 RoI", nameof(region));

        return fitMode switch
        {
            "exact" => new ShapeParameters { Geometry = new Polygon2d(contour, isClosed: true) },
            "fit-circle" => new ShapeParameters { Geometry = FitCircle(contour) },
            "fit-rect" => new ShapeParameters { Geometry = FitRotatedRect(contour) },
            "fit-bbox" => new ShapeParameters { Geometry = region.BoundingBox },
            _ => throw new NotSupportedException(
                $"不支持的 fitMode: '{fitMode}'（支持: exact / fit-circle / fit-rect / fit-bbox）")
        };
    }

    /// <summary>
    /// 将 RoI 栅格化为算法区域（零失真，用户认可的易方向）。
    /// 非面状几何（Line2d/Arc2d）无法构成封闭区域，直接报错（rule-015 不静默）。
    /// </summary>
    public static IVisionRegion RoiToRegion(ShapeParameters roi)
    {
        if (roi == null) throw new ArgumentNullException(nameof(roi));
        if (roi.Geometry == null)
            throw new ArgumentException("RoI 无几何数据，无法转换为区域", nameof(roi));

        HRegion region = roi.Geometry switch
        {
            Circle2d c => GenCircle(c.Center.Y, c.Center.X, c.Radius),
            RotatedRect2d rr => GenRectangle2(rr.Center.Y, rr.Center.X, rr.AngleRadians, rr.Width / 2, rr.Height / 2),
            System.Windows.Rect r => GenRectangle1(r.Top, r.Left, r.Bottom, r.Right),
            Polygon2d poly => GenPolygon(poly.Vertices),
            Annulus2d a => GenAnnulus(a),
            Line2d => throw new NotSupportedException("直线(Line2d)无法栅格化为封闭区域"),
            Arc2d => throw new NotSupportedException("弧(Arc2d)无法栅格化为封闭区域"),
            _ => throw new NotSupportedException($"不支持的几何类型: {roi.Geometry.GetType().Name}")
        };

        return new HalRegion(region);
    }

    /// <summary>
    /// 区域圆度 (0~1)，1 为正圆。调用方据以决定是否 fit-circle。
    /// 公式：4π·面积 / 周长²（周长取自外轮廓）。
    /// </summary>
    public static double RegionCircularity(IVisionRegion region)
    {
        if (region == null) throw new ArgumentNullException(nameof(region));
        double area = region.PixelCount;
        if (area <= 0) return 0;

        var contour = region.GetContour();
        if (contour.Length < 2) return 0;

        double perimeter = 0;
        for (int i = 0; i < contour.Length; i++)
        {
            var a = contour[i];
            var b = contour[(i + 1) % contour.Length];
            perimeter += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }
        if (perimeter <= 0) return 0;
        return 4 * Math.PI * area / (perimeter * perimeter);
    }

    #region 拟合辅助

    private static Circle2d FitCircle(Point[] contour)
    {
        double cx = contour.Average(p => p.X);
        double cy = contour.Average(p => p.Y);
        double r = contour.Max(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)));
        return new Circle2d(cx, cy, r);
    }

    private static RotatedRect2d FitRotatedRect(Point[] contour)
    {
        double cx = contour.Average(p => p.X);
        double cy = contour.Average(p => p.Y);
        int n = contour.Length;
        double sxx = 0, sxy = 0, syy = 0;
        foreach (var p in contour)
        {
            double dx = p.X - cx, dy = p.Y - cy;
            sxx += dx * dx; sxy += dx * dy; syy += dy * dy;
        }
        sxx /= n; sxy /= n; syy /= n;
        double theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        double cos = Math.Cos(theta);
        double sin = Math.Sin(theta);
        double maxX = 0, maxY = 0;
        foreach (var p in contour)
        {
            double dx = p.X - cx, dy = p.Y - cy;
            double x = dx * cos + dy * sin;
            double y = -dx * sin + dy * cos;
            maxX = Math.Max(maxX, Math.Abs(x));
            maxY = Math.Max(maxY, Math.Abs(y));
        }
        return new RotatedRect2d(cx, cy, 2 * maxX, 2 * maxY, theta * 180 / Math.PI);
    }

    #endregion

    #region Halcon 区域生成（rule-014: HOperatorSet 静态方法）

    private static HRegion GenCircle(double row, double col, double radius)
    {
        HOperatorSet.GenCircle(out var region, row, col, radius);
        return (HRegion)region;
    }

    private static HRegion GenRectangle1(double row1, double col1, double row2, double col2)
    {
        HOperatorSet.GenRectangle1(out var region, row1, col1, row2, col2);
        return (HRegion)region;
    }

    private static HRegion GenRectangle2(double row, double col, double phi, double length1, double length2)
    {
        HOperatorSet.GenRectangle2(out var region, row, col, phi, length1, length2);
        return (HRegion)region;
    }

    private static HRegion GenPolygon(Point[] vertices)
    {
        HTuple rows = new HTuple();
        HTuple cols = new HTuple();
        foreach (var p in vertices)
        {
            rows = rows.TupleConcat(p.Y);
            cols = cols.TupleConcat(p.X);
        }
        HOperatorSet.GenRegionPolygonFilled(out var region, rows, cols);
        return (HRegion)region;
    }

    private static HRegion GenAnnulus(Annulus2d a)
    {
        HOperatorSet.GenCircle(out var outer, a.Center.Y, a.Center.X, a.OuterRadius);
        if (a.InnerRadius <= 0)
            return (HRegion)outer;
        HOperatorSet.GenCircle(out var inner, a.Center.Y, a.Center.X, a.InnerRadius);
        HOperatorSet.Difference(outer, inner, out var diff);
        return (HRegion)diff;
    }

    #endregion
}
