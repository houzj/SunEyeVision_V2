using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 检测区域"声明式三层下钻"设计期契约测试：
/// 未运行/未绘制时，绑定树与结果树仍按 ShapeType 枚举出结构（类型→形状分组→属性字段），
/// 不依赖运行值、不读绘制数据（rule-016 数据驱动优先，rule-008 零耦合）。
/// 对应方案验收标准 AC-01/AC-02/AC-03 与防御机制"契约测试"。
/// </summary>
public class RoiDesignTimeGroupsContractTests
{
    private static AvailableDataSource MakeEmptyRoiDataSource()
    {
        return new AvailableDataSource
        {
            SourceNodeId = "Node1",
            SourceNodeName = "Blob分析",
            PropertyName = "检测区图形",
            DisplayName = "检测区图形",
            PropertyType = typeof(ShapeParameters[]),
            VariableType = "RoI",
            CurrentValue = null,   // 设计期无运行值
        };
    }

    /// <summary>ShapeType 定义的全部可绑定类型（跳过 None）应正好 8 类。</summary>
    private static IEnumerable<ShapeType> AllBindableShapeTypes()
    {
        return Enum.GetValues<ShapeType>().Where(t => t != ShapeType.None);
    }

    [Fact]
    public void BuildRoiDesignTimeGroups_EnumeratesAllShapeTypes_NoData()
    {
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiDesignTimeGroups(ds);

        // 整体 + 8 类分组（Point/Line/Circle/Rectangle/RotatedRectangle/Polygon/Annulus/Arc）
        Assert.Equal(9, children.Count);
        Assert.Equal(AllBindableShapeTypes().Count() + 1, children.Count);
    }

    [Fact]
    public void BuildRoiDesignTimeGroups_EveryGroup_Selectable_WithTypeSuffix()
    {
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiDesignTimeGroups(ds);

        foreach (var type in AllBindableShapeTypes())
        {
            var node = children.Single(c => c.Text == ArrayTreeFormatter.GetShapeTypeShortName(type));
            Assert.True(node.IsSelectable, $"{type} 分组应可绑定");
            Assert.NotNull(node.DataSource);
            Assert.Equal($"[Type={type}]", node.DataSource!.BindingSuffix);
            Assert.Equal($"Node1.检测区图形[Type={type}]", node.DataSource.GetBindingPath());
        }
    }

    [Fact]
    public void BuildRoiDesignTimeGroups_Overall_Selectable_WithWholeArrayPath()
    {
        // "整体"入口：绑定整个检测区域数组（BindingSuffix=null → 路径 "[]"）
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiDesignTimeGroups(ds);

        var overall = children[0];
        Assert.Equal("整体", overall.Text);
        Assert.True(overall.IsSelectable, "整体入口应可绑定");
        Assert.NotNull(overall.DataSource);
        Assert.Null(overall.DataSource!.BindingSuffix);
        Assert.Equal(typeof(ShapeParameters[]), overall.DataSource.PropertyType);
        Assert.Equal("Node1.检测区图形[]", overall.DataSource.GetBindingPath());
        Assert.Equal("检测区图形[]", overall.BindingPath);
    }

    [Fact]
    public void BuildRoiDesignTimeGroups_DoesNotDependOnCurrentValue()
    {
        // 设计期 CurrentValue 为 null，仍产出整体 + 全部 8 类分组（声明式，与绘制数据零耦合）
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiDesignTimeGroups(ds);

        Assert.Equal(9, children.Count);
        Assert.Contains(children, c => c.Text == "整体");
        Assert.Contains(children, c => c.Text == "圆");
        Assert.Contains(children, c => c.Text == "旋转矩形");
    }

    [Fact]
    public void CreateDefaultGeometry_EveryShapeType_ExposesMonitorFields()
    {
        // 结果树设计期属性行依赖 ShapeParameters.GetMonitorFields（与运行期同源）：
        // 圆 → 中心X/中心Y/半径；矩形(Rect) → X/Y/宽/高等字段兜底。
        foreach (var type in AllBindableShapeTypes())
        {
            var shape = new ShapeParameters { Geometry = RoiData.CreateDefaultGeometry(type) };
            Assert.NotNull(shape.Geometry);

            var fields = shape.GetMonitorFields();
            Assert.NotEmpty(fields);
            Assert.Contains(fields, f => !string.IsNullOrEmpty(f.FieldName));
        }
    }

    [Fact]
    public void ShapeParameters_GetMonitorFields_RectangleYieldsXYWidthHeight()
    {
        // 矩形(Rect) 走 ShapeParameters.GetMonitorFields 的 Rect 兜底，结果树设计期属性行不缺字段
        var shape = new ShapeParameters { Geometry = RoiData.CreateDefaultGeometry(ShapeType.Rectangle) };
        var fields = shape.GetMonitorFields();

        Assert.Contains(fields, f => f.FieldName == "X");
        Assert.Contains(fields, f => f.FieldName == "Y");
        Assert.Contains(fields, f => f.FieldName == "宽");
        Assert.Contains(fields, f => f.FieldName == "高");
    }

    [Fact]
    public void BuildTreeStructure_RoiArrayContainer_IsPureFolderNotSelectable()
    {
        // 方案B：RoI 数组容器节点是"纯分组容器"，本身不可整体绑定（IsSelectable=false），
        // 只能展开下钻；"整体"与各图形分组子节点仍可绑定。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }).Single();

        // 找到 RoI 数组叶子节点（容器）
        var container = root.Children.Single(c => c.Text == "检测区图形[]");
        Assert.False(container.IsSelectable, "RoI 数组容器节点不可整体绑定");
        Assert.True(container.HasChildren, "容器应可展开");
        Assert.NotNull(container.Children);

        // 子节点：整体 + 8 分组，全部可绑定
        Assert.Equal(9, container.Children!.Count);
        var overall = container.Children[0];
        Assert.Equal("整体", overall.Text);
        Assert.True(overall.IsSelectable, "整体入口应可绑定");
        Assert.Equal("检测区图形[]", overall.BindingPath);
        Assert.Equal("Node1.检测区图形[]", overall.DataSource!.GetBindingPath());

        Assert.All(container.Children.Skip(1), g =>
        {
            Assert.True(g.IsSelectable, $"分组 '{g.Text}' 应可绑定");
            Assert.NotNull(g.DataSource);
        });
    }

    [Fact]
    public void BuildTreeStructure_RoiChildNodes_HaveParentChain()
    {
        // P1：RoI 子节点（整体/分组）必须挂上 Parent 链，否则 GetRootNode 向上遍历到 null 时
        // 把分组自身当根，显示名根节点取错（圆.xxx 而非 Blob分析.xxx）。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }).Single();
        var container = root.Children.Single(c => c.Text == "检测区图形[]");

        // 容器 → 根
        Assert.Same(root, container.Parent);
        // 整体节点 → 容器
        Assert.Same(container, container.Children[0].Parent);
        // 分组节点 → 容器（P3：分组不显示数量，文本即短名）
        Assert.Same(container, container.Children.First(c => c.Text == "圆").Parent);
        Assert.Same(container, container.Children.First(c => c.Text == "旋转矩形").Parent);

        // 沿 Parent 链向上能走到根节点（Blob分析）——验证显示名根节点正确
        var circleGroup = container.Children.First(c => c.Text == "圆");
        TreeNodeData top = circleGroup;
        while (top.Parent != null)
            top = top.Parent;
        Assert.Same(root, top);
        Assert.Equal("Blob分析", top.Text);
    }
}
