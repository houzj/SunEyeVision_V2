using System;
using System.Linq;
using System.Windows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// RegionConverter 契约测试：Region↔RoI 桥接（方案 D8 + 验收 #6）。
/// 用 FakeRegion 规避 Halcon 运行时依赖，覆盖 exact/fit/报错/圆度。
/// RoiToRegion 依赖 Halcon 区域生成，由集成/端到端阶段验证。
/// </summary>
public class RegionConverterTests
{
    #region Fake 区域（不依赖 Halcon）

    private sealed class FakeRegion : IVisionRegion
    {
        private readonly Point[] _contour;
        private readonly Rect _bbox;
        private readonly int _pixelCount;

        public FakeRegion(Point[] contour, Rect bbox, int pixelCount)
        {
            _contour = contour;
            _bbox = bbox;
            _pixelCount = pixelCount;
        }

        public int PixelCount => _pixelCount;
        public Rect BoundingBox => _bbox;
        public bool IsEmpty => false;
        public Point[] GetContour() => _contour;
        public byte[] GetMaskData() => Array.Empty<byte>();
        public IVisionRegion Clone() => this;
        public void Dispose() { }
    }

    private static Point[] SquareContour(double x, double y, double size)
    {
        return new[]
        {
            new Point(x, y),
            new Point(x + size, y),
            new Point(x + size, y + size),
            new Point(x, y + size),
        };
    }

    private static Point[] CircleContour(Point center, double radius, int n = 64)
    {
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            double a = 2 * Math.PI * i / n;
            pts[i] = new Point(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a));
        }
        return pts;
    }

    #endregion

    #region RegionToRoi exact（零失真）

    [Fact]
    public void RegionToRoi_Exact_ReturnsPolygonWithSameContour()
    {
        var contour = SquareContour(10, 20, 100);
        var region = new FakeRegion(contour, new Rect(10, 20, 100, 100), 10000);

        var roi = RegionConverter.RegionToRoi(region, "exact");

        var poly = Assert.IsType<Polygon2d>(roi.Geometry);
        Assert.True(poly.IsClosed);
        Assert.Equal(contour.Length, poly.Vertices.Length);
        for (int i = 0; i < contour.Length; i++)
        {
            Assert.Equal(contour[i].X, poly.Vertices[i].X);
            Assert.Equal(contour[i].Y, poly.Vertices[i].Y);
        }
    }

    #endregion

    #region RegionToRoi 报错优先（rule-015）

    [Fact]
    public void RegionToRoi_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RegionConverter.RegionToRoi(null!));
    }

    [Fact]
    public void RegionToRoi_Empty_Throws()
    {
        var empty = new FakeEmptyRegion();
        Assert.Throws<ArgumentException>(() => RegionConverter.RegionToRoi(empty));
    }

    [Fact]
    public void RegionToRoi_UnknownFitMode_Throws()
    {
        var region = new FakeRegion(SquareContour(0, 0, 10), new Rect(0, 0, 10, 10), 100);
        Assert.Throws<NotSupportedException>(() => RegionConverter.RegionToRoi(region, "fit-unknown"));
    }

    private sealed class FakeEmptyRegion : IVisionRegion
    {
        public int PixelCount => 0;
        public Rect BoundingBox => Rect.Empty;
        public bool IsEmpty => true;
        public Point[] GetContour() => Array.Empty<Point>();
        public byte[] GetMaskData() => Array.Empty<byte>();
        public IVisionRegion Clone() => this;
        public void Dispose() { }
    }

    #endregion

    #region RegionToRoi fit 显式 opt-in

    [Fact]
    public void RegionToRoi_FitCircle_ReturnsCircle()
    {
        var center = new Point(50, 50);
        var contour = CircleContour(center, 20);
        var region = new FakeRegion(contour, new Rect(30, 30, 40, 40), 1200);

        var roi = RegionConverter.RegionToRoi(region, "fit-circle");
        var c = Assert.IsType<Circle2d>(roi.Geometry);
        Assert.Equal(50, c.Center.X, 1);
        Assert.Equal(50, c.Center.Y, 1);
        Assert.Equal(20, c.Radius, 1);
    }

    [Fact]
    public void RegionToRoi_FitBBox_ReturnsRect()
    {
        var region = new FakeRegion(SquareContour(10, 20, 100), new Rect(10, 20, 100, 100), 10000);
        var roi = RegionConverter.RegionToRoi(region, "fit-bbox");
        var r = Assert.IsType<Rect>(roi.Geometry);
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(100, r.Width);
        Assert.Equal(100, r.Height);
    }

    #endregion

    #region RegionCircularity

    [Fact]
    public void RegionCircularity_Circle_IsNearOne()
    {
        var contour = CircleContour(new Point(0, 0), 30);
        var region = new FakeRegion(contour, new Rect(-30, -30, 60, 60), 2800);
        var circ = RegionConverter.RegionCircularity(region);
        Assert.True(circ > 0.95, $"圆度应接近 1，实际 {circ}");
    }

    [Fact]
    public void RegionCircularity_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RegionConverter.RegionCircularity(null!));
    }

    #endregion
}
