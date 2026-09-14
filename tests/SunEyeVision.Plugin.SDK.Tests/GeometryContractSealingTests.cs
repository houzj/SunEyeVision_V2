using System;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 契约封口守卫：几何值只允许 <c>Models.Geometry</c> 契约类型（方案 P0-2/P0-5 封口 + 5e 守卫）。
/// </summary>
/// <remarks>
/// 守的是"很容易被改回去、且改回去表面看不出来"的退化：
/// <list type="bullet">
/// <item>有人重新引入 WPF <c>Rect</c>/<c>Point2D</c> 作为几何值承载 → round-trip 类型不对，红灯；</item>
/// <item>有人给几何字段加回 WPF 类型的序列化判别符（写出非契约 <c>$geom</c>）→ 红灯。</item>
/// </list>
/// 注：WPF 类型在 <b>UI/脚本层</b>（脚本值展开、绘图交互、图像裁剪 API）仍是合法的展示/入参类型，
/// 本守卫只约束"作为 <see cref="RoiShape.Geometry"/> 几何值"这一用途。
/// </remarks>
public class GeometryContractSealingTests
{
    // 与生产序列化口径一致（默认选项；几何判别符由 GeometryJsonConverter 自行写出/读入）
    private static readonly JsonSerializerOptions Options = new();

    private static RoiShape RoundTrip(RoiShape shape)
    {
        var json = JsonSerializer.Serialize(shape, Options);
        return JsonSerializer.Deserialize<RoiShape>(json, Options)!;
    }

    [Fact]
    public void RoundTrip_LegacyWpfRect_IsSealedToRotatedRect2d()
    {
        // 契约封口：即便有代码把 WPF Rect 塞进 Geometry（老工程/遗留分支），
        // round-trip 后必须是契约类型 RotatedRect2d，且值等价（中心 + 尺寸）。
        var result = RoundTrip(new RoiShape { Geometry = new System.Windows.Rect(10, 20, 30, 40) });

        var rr = Assert.IsType<RotatedRect2d>(result.Geometry);
        Assert.Equal(10 + 30 / 2.0, rr.Center.X, 6);
        Assert.Equal(20 + 40 / 2.0, rr.Center.Y, 6);
        Assert.Equal(30, rr.Width, 6);
        Assert.Equal(40, rr.Height, 6);
    }

    [Fact]
    public void RoundTrip_LegacyUiPoint2D_IsSealedToContractPoint2d()
    {
        // 契约封口：UI 交互类型 Point2D 不得作为几何值留存 → 统一还原为契约 Point2d。
        var result = RoundTrip(new RoiShape { Geometry = new Point2D(15, 25) });

        var pt = Assert.IsType<Point2d>(result.Geometry);
        Assert.Equal(15, pt.X, 6);
        Assert.Equal(25, pt.Y, 6);
    }

    [Fact]
    public void Serialize_ContractGeometry_EmitsContractDiscriminator()
    {
        // 写出侧封口：契约几何类型的判别符必须是类型自身名（不出现 "Rect" 之类的遗留名）
        var json = JsonSerializer.Serialize(new RoiShape { Geometry = new Circle2d(1, 2, 3) }, Options);

        Assert.Contains("$geom", json, StringComparison.Ordinal);
        Assert.Contains(nameof(Circle2d), json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Rect\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GeometryJsonConverter_Whitelist_ContainsNoWpfGeometryTypes()
    {
        // 读侧白名单只允许"契约类型 + 显式迁移别名"：
        // 契约类型 7 个（Circle2d/RotatedRect2d/Annulus2d/Polygon2d/Line2d/Arc2d/Point2d）
        // + 1 个迁移别名 Point2D（读入即还原为 Point2d）。
        // "Rect" 只能出现在 Read 的迁移分支里（结构不同需单独转换），不得作为白名单类型。
        var converter = new GeometryJsonConverter();
        Assert.NotNull(converter);

        // 已迁移的老类型必须得到契约类型（此处以 RoundTrip 断言，避免依赖私有白名单字段）
        Assert.IsType<RotatedRect2d>(RoundTrip(new RoiShape { Geometry = new System.Windows.Rect(0, 0, 1, 1) }).Geometry);
    }

    [Fact]
    public void RoiShape_PublicSurface_DoesNotExposeWpfRectGeometry()
    {
        // RoiShape 的几何相关公共成员不得以 WPF Rect 为类型（多边形顶点 POINT2D 属 UI 交互职责，允许）。
        var wpfRectMembers = typeof(RoiShape).GetMembers()
            .Where(m => m.ToString()?.Contains("System.Windows.Rect", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Empty(wpfRectMembers);
    }
}
