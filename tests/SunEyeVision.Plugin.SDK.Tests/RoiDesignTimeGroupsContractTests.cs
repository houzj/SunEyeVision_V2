using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 绑定树 RoI 子树"按 DataType 词目定型"契约测试（绑定树 DataType 定型方案）：
/// - ROI/未限定目标 → 各类型集合叶（绑定终点全为叶子，无字段下钻、无 [i] 层级；
///   **「整体」已取消**，见方案 §11）；
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
    public void BuildRoiTypedChildren_RoiTarget_YieldsAllTypeLeaves()
    {
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        // 全部形状叶（Point/Line/Circle/RotatedRectangle/Polygon/Annulus/Arc；Rectangle 已并入 RotatedRectangle）
        // 「整体」已取消（方案 §11），故不再 +1
        Assert.Equal(AllBindableShapeTypes().Count(), children.Count);
        Assert.DoesNotContain(children, c => c.Text == "整体");
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
    public void BuildRoiTypedChildren_RoiTarget_NoOverallEntry()
    {
        // §11 守卫：ROI 目标下**不得**再产出「整体」叶——它原为"上游一节点绘多种图形 → 一把全绑"
        // 的异构场景出口；订阅方只绑固定形状后语义已废（决策 1/2）。
        // 取消只影响"选择器里能否选"：老工程已绑的整体路径仍可解析、值不丢。
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        Assert.DoesNotContain(children, c => c.Text == "整体");
        // 所有叶都必须是类型集合叶（带 [Type=X] 后缀），不存在"无后缀的整体叶"
        Assert.All(children, c => Assert.False(string.IsNullOrEmpty(c.DataSource!.BindingSuffix),
            $"'{c.Text}' 缺 [Type=X] 后缀，疑似整体叶残留"));
    }

    [Theory]
    [InlineData(ShapeType.Point)]
    [InlineData(ShapeType.Line)]
    [InlineData(ShapeType.Circle)]
    [InlineData(ShapeType.RotatedRectangle)]
    [InlineData(ShapeType.Polygon)]
    [InlineData(ShapeType.Annulus)]
    [InlineData(ShapeType.Arc)]
    public void BuildRoiTypedChildren_RoiTarget_WithShapeFilter_YieldsSingleShapeLeaf(ShapeType filter)
    {
        // §11 守卫①：区域订阅选择「图形类型」后，候选必须收敛为**唯一一片**该形状的叶。
        // 防退化：改收敛逻辑时漏传 / 写反筛选条件 → 下拉又会回到"一串形状"，即用户最初报的问题复发。
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI", filter);

        var leaf = Assert.Single(children);
        Assert.Equal(ArrayTreeFormatter.GetShapeTypeShortName(filter), leaf.Text);
        Assert.True(leaf.IsSelectable, "筛选后的类型叶应是绑定终点");
        Assert.Equal($"[Type={filter}]", leaf.DataSource!.BindingSuffix);
        Assert.Equal($"Node1.检测区图形[Type={filter}]", leaf.DataSource.GetBindingPath());
        Assert.Empty(leaf.Children);
    }

    [Fact]
    public void BuildRoiTypedChildren_GeometryTarget_WithConflictingShapeFilter_Throws()
    {
        // §11 守卫②：目标词目**已定形状**（Point → 只允许"点"）时再叠加"圆"筛选必须**报错**。
        // 防退化：把冲突改成静默忽略 → 用户选了圆却绑不上，只在日志里留 Warning（回到"能选不能用"）。
        var ds = MakeEmptyRoiDataSource();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ArrayTreeFormatter.BuildRoiTypedChildren(ds, "Point", ShapeType.Circle));
        Assert.Contains("冲突", ex.Message);
    }

    [Fact]
    public void BuildRoiTypedChildren_GeometryTarget_MatchingShapeFilter_IsAllowed()
    {
        // 守卫②的边界：筛选与词目允许形状**一致**时不算冲突（幂等），仍只出一片叶。
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "Point", ShapeType.Point);

        var leaf = Assert.Single(children);
        Assert.Equal("点", leaf.Text);
        Assert.Equal("[Type=Point]", leaf.DataSource!.BindingSuffix);
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
        // 设计期 CurrentValue 为 null，仍产出全部形状叶（声明式，与绘制数据零耦合；「整体」已取消）
        var ds = MakeEmptyRoiDataSource();
        var roiChildren = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        Assert.Equal(AllBindableShapeTypes().Count(), roiChildren.Count);
        Assert.DoesNotContain(roiChildren, c => c.Text == "整体");
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
    public void RoiShape_GetMonitorFields_RotatedRectYieldsCenterWidthHeightAngle()
    {
        // P0-2 后矩形几何统一为 RotatedRect2d（WPF Rect 已退场），监控字段随契约类型走：
        // 中心X/中心Y/宽/高/角度（RotatedRect2d.GetMonitorFields），结果树设计期属性行不缺字段。
        var shape = new RoiShape { Geometry = RoiShape.CreateDefaultGeometry(ShapeType.RotatedRectangle) };
        var fields = shape.GetMonitorFields();

        Assert.Contains(fields, f => f.FieldName == "中心X");
        Assert.Contains(fields, f => f.FieldName == "中心Y");
        Assert.Contains(fields, f => f.FieldName == "宽");
        Assert.Contains(fields, f => f.FieldName == "高");
        Assert.Contains(fields, f => f.FieldName == "角度");
    }

    [Fact]
    public void BuildRoiTypedChildren_GeometryTarget_YieldsOnlyMatchingShapeLeaf_AndNoOverall()
    {
        // 设计收敛（改动 4）：几何值目标（Point）时 ROI 容器下钻**只出"点"**，且不出"整体"。
        // 依据 ShapeTypeVocabulary；"整体"是 RoiShape[]，绑不到 Point2d[]，属"能选不能用"。
        var ds = MakeEmptyRoiDataSource();
        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "Point");

        Assert.Single(children);
        Assert.DoesNotContain(children, c => c.Text == "整体");

        var pointLeaf = children[0];
        Assert.Equal("点", pointLeaf.Text);
        Assert.True(pointLeaf.IsSelectable);
        Assert.Equal("[Type=Point]", pointLeaf.DataSource!.BindingSuffix);
        Assert.Equal("Node1.检测区图形[Type=Point]", pointLeaf.DataSource.GetBindingPath());
    }

    [Fact]
    public void BuildRoiTypedChildren_EachGeometryVocabulary_YieldsItsOwnShapeOnly()
    {
        // 逐词目收敛：每个几何值词目只放行语义等价的形状叶（ShapeTypeVocabulary 唯一真相源）
        var ds = MakeEmptyRoiDataSource();

        foreach (var (vocabulary, shapeType) in new (string, ShapeType)[]
        {
            ("Point", ShapeType.Point),
            ("Line", ShapeType.Line),
            ("Circle", ShapeType.Circle),
            ("RotatedRect", ShapeType.RotatedRectangle),
            ("Polygon", ShapeType.Polygon),
            ("Annulus", ShapeType.Annulus),
            ("Arc", ShapeType.Arc),
        })
        {
            var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, vocabulary);

            Assert.Single(children);
            Assert.Equal(ArrayTreeFormatter.GetShapeTypeShortName(shapeType), children[0].Text);
            Assert.Equal($"[Type={shapeType}]", children[0].DataSource!.BindingSuffix);
        }
    }

    [Fact]
    public void BuildTreeStructure_RoiArrayContainer_IsPureFolderNotSelectable_RoiTarget()
    {
        // 方案B + §11：「整体」入口已取消——RoI 数组容器只作纯分组容器（IsSelectable=false），
        // 子节点全为类型集合叶（可绑定、无 children）。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }, typeof(RoiShape[])).Single();

        // 找到 RoI 数组叶子节点（容器）
        var container = root.Children.Single(c => c.Text == "检测区图形[]");
        Assert.False(container.IsSelectable, "RoI 数组容器节点不可整体绑定");
        Assert.True(container.HasChildren, "容器应可展开");
        Assert.NotNull(container.Children);

        // 子节点：全部形状叶（**无「整体」**——异构时代入口已废，见方案 §11）
        Assert.Equal(AllBindableShapeTypes().Count(), container.Children!.Count);
        Assert.DoesNotContain(container.Children!, c => c.Text == "整体");

        Assert.All(container.Children, g =>
        {
            Assert.True(g.IsSelectable, $"类型集合叶 '{g.Text}' 应可绑定");
            Assert.NotNull(g.DataSource);
            Assert.Empty(g.Children);
        });
    }

    [Fact]
    public void BuildTreeStructure_RoiChildNodes_HaveParentChain()
    {
        // P1：RoI 子节点（类型集合叶）必须挂上 Parent 链，否则 GetRootNode 向上遍历到 null 时
        // 把类型叶自身当根，显示名根节点取错（圆.xxx 而非 Blob分析.xxx）。
        var ds = MakeEmptyRoiDataSource();
        var root = ParamBindingBase.BuildTreeStructure(new List<AvailableDataSource> { ds }, typeof(RoiShape[])).Single();
        var container = root.Children.Single(c => c.Text == "检测区图形[]");

        // 容器 → 根
        Assert.Same(root, container.Parent);
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
