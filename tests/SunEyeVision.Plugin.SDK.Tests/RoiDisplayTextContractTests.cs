using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// RoI(ShapeParameters) 显示文本契约测试：ToString 输出中文短名（而非 CLR 类型全名）。
/// 对应方案验收标准：绑定下拉/下钻子节点/折叠摘要不再出现
/// "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters" 类型全名。
/// </summary>
public class RoiDisplayTextContractTests
{
    [Fact]
    public void ShapeParameters_ToString_Circle_RendersChineseShortName()
    {
        var sp = new ShapeParameters { Geometry = new Circle2d(new Point(50, 60), 25) };
        var text = sp.ToString();
        Assert.Contains("圆", text);
        Assert.DoesNotContain("ShapeParameters", text);
        Assert.DoesNotContain(nameof(SunEyeVision), text);
    }

    [Fact]
    public void ShapeParameters_ToString_RotatedRect_RendersChineseShortName()
    {
        var sp = new ShapeParameters { Geometry = new RotatedRect2d(100, 110, 20, 30, 15) };
        var text = sp.ToString();
        Assert.Contains("旋转矩形", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_SystemRect_RendersChineseShortName()
    {
        var sp = new ShapeParameters { Geometry = new Rect(10, 20, 30, 40) };
        var text = sp.ToString();
        Assert.Contains("矩形", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_Polygon_RendersVertexCount()
    {
        var sp = new ShapeParameters
        {
            Geometry = new Polygon2d(new[]
            {
                new Point(0, 0), new Point(10, 0), new Point(5, 10),
            })
        };
        var text = sp.ToString();
        Assert.Contains("多边形", text);
        Assert.Contains("3", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_Line_RendersPointAndDirection()
    {
        var sp = new ShapeParameters
        {
            Geometry = Line2d.FromTwoPoints(new Point(0, 0), new Point(10, 0)),
        };
        var text = sp.ToString();
        Assert.Contains("直线", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_Annulus_RendersRadii()
    {
        var sp = new ShapeParameters { Geometry = new Annulus2d(5, 6, 7, 8, 0.5, 2.0) };
        var text = sp.ToString();
        Assert.Contains("圆环", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_Arc_RendersChineseShortName()
    {
        var sp = new ShapeParameters { Geometry = new Arc2d(1, 2, 3, 0.1, 1.2) };
        var text = sp.ToString();
        Assert.Contains("弧形", text);
        Assert.DoesNotContain("ShapeParameters", text);
    }

    [Fact]
    public void ShapeParameters_ToString_NullGeometry_RendersEmptyShape()
    {
        var sp = new ShapeParameters { Geometry = null };
        Assert.Equal("空图形", sp.ToString());
    }

    [Fact]
    public void FormatElementSummary_RoiElement_NoClrTypeFullName()
    {
        // 折叠摘要/逐元素 [i] 走 FormatElementSummary → ShapeParameters.ToString()，
        // 必须输出中文短名而非 CLR 类型全名（根因 1 治本验证）。
        var summary = ArrayTreeFormatter.FormatElementSummary(new ShapeParameters
        {
            Geometry = new Circle2d(new Point(50, 60), 25),
        });

        Assert.Contains("圆", summary);
        Assert.DoesNotContain("ShapeParameters", summary);
        Assert.DoesNotContain(nameof(SunEyeVision), summary);
    }

    [Fact]
    public void FormatElementSummary_EmptyRoi_ReturnsEmptyMark()
    {
        // 空 RoI 数组折叠摘要返回 "(空)"，不返回类型全名。
        Assert.Equal("(空)", ArrayTreeFormatter.FormatCollapsedSummary(Array.Empty<ShapeParameters>()));
    }
}
