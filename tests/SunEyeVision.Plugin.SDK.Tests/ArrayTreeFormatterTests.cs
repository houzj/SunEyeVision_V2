using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 数组树显示助手测试（方案步骤6 / 〇 原则3）：折叠单行 `;` 串联 + 展开字段聚合/逐元素封顶。
/// </summary>
public class ArrayTreeFormatterTests
{
    [Fact]
    public void FormatCollapsedSummary_JoinsWithSemicolon()
    {
        var arr = new double[] { 1.0, 2.0, 3.0 };
        Assert.Equal("1; 2; 3", ArrayTreeFormatter.FormatCollapsedSummary(arr));
    }

    [Fact]
    public void FormatCollapsedSummary_CapsAndEllipsis()
    {
        var arr = Enumerable.Range(0, 200).Select(i => (double)i).ToArray();
        var s = ArrayTreeFormatter.FormatCollapsedSummary(arr);
        Assert.Contains("…", s);
    }

    [Fact]
    public void BuildExpansionChildren_RoIAggregatesByField()
    {
        // RoI[] 按字段聚合：每个几何字段一个节点，值 = 跨所有 RoI 元素 `;` 串联
        var roi = new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new Circle2d(100, 80, 50) },
            new ShapeParameters { Geometry = new Circle2d(200, 90, 60) },
        };
        var children = ArrayTreeFormatter.BuildExpansionChildren(roi, typeof(ShapeParameters));

        // 字段节点数 ≥ 几何字段数（圆心X/圆心Y/半径…）
        Assert.True(children.Count >= 2);

        // 至少存在一个字段节点横跨两个 RoI 元素（含 100 与 200 且用 ; 串联）
        var spanNode = children.FirstOrDefault(c =>
            c.Text.Contains("100") && c.Text.Contains("200") && c.Text.Contains(";"));
        Assert.NotNull(spanNode);
    }

    [Fact]
    public void BuildExpansionChildren_NumericArray_CappedWithOverflow()
    {
        // 数值数组展开封顶防炸：100 个元素 + 1 个溢出节点
        var arr = Enumerable.Range(0, 1000).Select(i => (double)i).ToArray();
        var children = ArrayTreeFormatter.BuildExpansionChildren(arr, typeof(double));
        Assert.Equal(ArrayTreeFormatter.ExpansionCap + 1, children.Count);
        Assert.Contains("还有更多", children[^1].Text);
    }
}
