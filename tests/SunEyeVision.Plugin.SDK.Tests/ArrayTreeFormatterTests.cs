using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 数组树显示助手测试（方案步骤6 / 〇 原则3）：折叠单行 `;` 串联 + 逐元素封顶。
/// RoI(RoiShape[]) 绑定树由 BuildRoiTypedChildren 按目标词目定型（见 RoiDesignTimeGroupsContractTests），
/// 不在此展开。
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
    public void BuildExpansionChildren_NumericArray_CappedWithOverflow()
    {
        // 数值数组展开封顶防炸：100 个元素 + 1 个溢出节点
        var arr = Enumerable.Range(0, 1000).Select(i => (double)i).ToArray();
        var children = ArrayTreeFormatter.BuildExpansionChildren(arr, typeof(double));
        Assert.Equal(ArrayTreeFormatter.ExpansionCap + 1, children.Count);
        Assert.Contains("还有更多", children[^1].Text);
    }
}
