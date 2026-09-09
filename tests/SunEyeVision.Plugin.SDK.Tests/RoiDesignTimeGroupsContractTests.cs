using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 绑定树 RoI 子树"按 DataType 词目定型"契约测试（绑定树 DataType 定型方案）：
/// - ROI/未限定目标 → 整体叶 + 各类型集合叶（绑定终点全为叶子，无字段下钻、无 [i] 层级）；
/// - 数值目标（Double/Integer）→ 类型容器(不可绑,只导航) + RoiFieldSchema 字段叶。
/// 声明式由 ShapeType 枚举驱动，不依赖运行值/绘制数据（rule-016 数据驱动优先）。
/// 对应方案验收标准 AC-01/AC-02 与防御机制"契约测试"。
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
            PropertyType = typeof(RoiShape[]),
            VariableType = "ROI",
            CurrentValue = null,   // 设计期无运行值
        };
    }

    /// <summary>ShapeType 定义的全部可绑定类型（跳过 None）应正好 8 类。</summary>
    private static IEnumerable<ShapeType> AllBindableShapeTypes()
    {
        return Enum.GetValues<ShapeType>().Where(t => t != ShapeType.None);
    }

    [Fact]
    public void BuildRoiTypedChildren_RoiTarget_YieldsOverallPlusAllTypeLeaves()
    {
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        // 整体 + 8 类类型集合叶（Point/Line/Circle/Rectangle/RotatedRectangle/Polygon/Annulus/Arc）
        Assert.Equal(9, children.Count);
        Assert.Equal(AllBindableShapeTypes().Count() + 1, children.Count);
    }

    [Fact]
    public void BuildRoiTypedChildren_RoiTarget_EveryTypeIsLeaf_Selectable_WithSuffix()
    {
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        foreach (var type in AllBindableShapeTypes())
        {
            var node = children.Single(c => c.Text == ArrayTreeFormatter.GetShapeTypeShortName(type));
            Assert.True(node.IsSelectable, $"{type} 类型集合叶应可绑定");
            Assert.NotNull(node.DataSource);
            Assert.Equal($"[Type={type}]", node.DataSource!.BindingSuffix);
            Assert.Equal($"Node1.检测区图形[Type={type}]", node.DataSource.GetBindingPath());
            // 绑定终点必须无 children（无"可展开 + 可绑定"角色冲突）
            Assert.Empty(node.Children);
        }
    }

    [Fact]
    public void BuildRoiTypedChildren_RoiTarget_Overall_Selectable_WithWholeArrayPath()
    {
        // "整体"入口：绑定整个检测区域数组（BindingSuffix=null → 路径 "[]"）
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        var overall = children[0];
        Assert.Equal("整体", overall.Text);
        Assert.True(overall.IsSelectable, "整体入口应可绑定");
        Assert.NotNull(overall.DataSource);
        Assert.Null(overall.DataSource!.BindingSuffix);
        Assert.Equal(typeof(RoiShape[]), overall.DataSource.PropertyType);
        Assert.Equal("Node1.检测区图形[]", overall.DataSource.GetBindingPath());
        Assert.Equal("检测区图形[]", overall.BindingPath);
        Assert.Empty(overall.Children);
    }

    [Fact]
    public void BuildRoiTypedChildren_NumericTarget_YieldsTypeContainersWithFieldLeaves()
    {
        // double 目标：无整体/类型集合叶（ROI 绑不上 double）；类型=容器(不可绑)，children=字段叶(可绑)
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "Double");

        Assert.DoesNotContain(children, c => c.Text == "整体");
        Assert.NotEmpty(children);

        // 圆容器：不可选（纯导航），字段叶（圆心X/圆心Y/半径）可选
        var circle = children.Single(c => c.Text == "圆");
        Assert.False(circle.IsSelectable, "类型容器不可绑定（只导航）");
        Assert.NotEmpty(circle.Children);

        var radius = circle.Children!.Single(c => c.Text == "半径");
        Assert.True(radius.IsSelectable, "字段叶应可绑定");
        // 数组集合语义：不自动填索引（[Type=Circle][] = 全部圆的该字段；需第 N 个由用户手写）
        Assert.Equal("[Type=Circle][].Geometry.Radius", radius.DataSource!.BindingSuffix);
        Assert.Equal("Node1.检测区图形[Type=Circle][].Geometry.Radius", radius.DataSource.GetBindingPath());
        Assert.Empty(radius.Children);

        // 容器自身不提供集合绑定（children 全部为字段叶，无 [Type=X] 容器级绑定）
        Assert.All(circle.Children, f => Assert.True(f.IsSelectable));
    }

    [Fact]
    public void BuildRoiTypedChildren_DoesNotDependOnCurrentValue()
    {
        // 设计期 CurrentValue 为 null，仍产出整体 + 全部 8 类类型叶（声明式，与绘制数据零耦合）
        var ds = MakeEmptyRoiDataSource();
        var roiChildren = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        Assert.Equal(9, roiChildren.Count);
        Assert.Contains(roiChildren, c => c.Text == "整体");
        Assert.Contains(roiChildren, c => c.Text == "圆");
        Assert.Contains(roiChildren, c => c.Text == "旋转矩形");
    }

    [Fact]
    public void CreateDefaultGeometry_EveryShapeType_ExposesMonitorFields()
    {
        // 结果树设计期属性行依赖 RoiShape.GetMonitorFields（与运行期同源）：
        // 圆 → 中心X/中心Y/半径；矩形(Rect) → X/Y/宽/高等字段兜底。
        foreach (var type in AllBindableShapeTypes())
        {
            var shape = new RoiShape { Geometry = RoiShape.CreateDefaultGeometry(type) };
            Assert.NotNull(shape.Geometry);

            var fields = shape.GetMonitorFields();
            Assert.NotEmpty(fields);
            Assert.Contains(fields, f => !string.IsNullOrEmpty(f.FieldName));
        }
    }

    [Fact]
    public void RoiShape_GetMonitorFields_RectangleYieldsXYWidthHeight()
    {
        // 矩形(Rect) 走 RoiShape.GetMonitorFields 的 Rect 兜底，结果树设计期属性行不缺字段
        var shape = new RoiShape { Geometry = RoiShape.CreateDefaultGeometry(ShapeType.Rectangle) };
        var fields = shape.GetMonitorFields();

        Assert.Contains(fields, f => f.FieldName == "X");
        Assert.Contains(fields, f => f.FieldName == "Y");
        Assert.Contains(fields, f => f.FieldName == "宽");
        Assert.Contains(fields, f => f.FieldName == "高");
    }

    [Fact]
    public void BuildTreeStructure_RoiArrayContainer_IsPureFolderNotSelectable_RoiTarget()
    {
        // 方案B：RoI 数组容器节点是"纯分组容器"，本身不可整体绑定（IsSelectable=false），
        // 只能展开下钻；整体与各类型集合叶子节点仍可绑定（ROI 目标）。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }, typeof(RoiShape[])).Single();

        // 找到 RoI 数组叶子节点（容器）
        var container = root.Children.Single(c => c.Text == "检测区图形[]");
        Assert.False(container.IsSelectable, "RoI 数组容器节点不可整体绑定");
        Assert.True(container.HasChildren, "容器应可展开");
        Assert.NotNull(container.Children);

        // 子节点：整体 + 8 类型集合叶，全部为叶子（可绑定，无 children）
        Assert.Equal(9, container.Children!.Count);
        var overall = container.Children[0];
        Assert.Equal("整体", overall.Text);
        Assert.True(overall.IsSelectable, "整体入口应可绑定");
        Assert.Equal("检测区图形[]", overall.BindingPath);
        Assert.Equal("Node1.检测区图形[]", overall.DataSource!.GetBindingPath());

        Assert.All(container.Children.Skip(1), g =>
        {
            Assert.True(g.IsSelectable, $"类型集合叶 '{g.Text}' 应可绑定");
            Assert.NotNull(g.DataSource);
            Assert.Empty(g.Children);
        });
    }

    [Fact]
    public void BuildTreeStructure_RoiChildNodes_HaveParentChain()
    {
        // P1：RoI 子节点（整体/类型集合叶）必须挂上 Parent 链，否则 GetRootNode 向上遍历到 null 时
        // 把类型叶自身当根，显示名根节点取错（圆.xxx 而非 Blob分析.xxx）。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }, typeof(RoiShape[])).Single();
        var container = root.Children.Single(c => c.Text == "检测区图形[]");

        // 容器 → 根
        Assert.Same(root, container.Parent);
        // 整体节点 → 容器
        Assert.Same(container, container.Children[0].Parent);
        // 类型集合叶 → 容器（P3：类型不显示数量，文本即短名）
        Assert.Same(container, container.Children.First(c => c.Text == "圆").Parent);
        Assert.Same(container, container.Children.First(c => c.Text == "旋转矩形").Parent);

        // 沿 Parent 链向上能走到根节点（Blob分析）——验证显示名根节点正确
        var circleLeaf = container.Children.First(c => c.Text == "圆");
        TreeNodeData top = circleLeaf;
        while (top.Parent != null)
            top = top.Parent;
        Assert.Same(root, top);
        Assert.Equal("Blob分析", top.Text);
    }
}
