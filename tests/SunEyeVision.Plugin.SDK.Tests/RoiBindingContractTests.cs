using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// RoI 多图形统一绑定契约测试（绑定树 DataType 定型方案）：
/// 绑定树不再展开逐元素 [i]（声明式类型树，无字段聚合错位），运行期 [Type=] 过滤保留（SSOT）。
/// 对应方案验收标准 AC-01/AC-03/AC-04 与防御机制"契约测试 + 解析器单测"。
/// </summary>
public class RoiBindingContractTests
{
    private static RoiShape[] MakeHeterogeneousRoi()
    {
        return new RoiShape[]
        {
            new RoiShape { Geometry = new Circle2d(100, 80, 50) },
            new RoiShape { Geometry = new Circle2d(200, 80, 30) },
            new RoiShape { Geometry = new RotatedRect2d(10, 20, 30, 40, 15) },
            new RoiShape { Geometry = new RotatedRect2d(50, 60, 30, 40, 15) },
            new RoiShape { Geometry = new Polygon2d(new[] { new System.Windows.Point(0, 0), new System.Windows.Point(10, 0), new System.Windows.Point(5, 10) }) },
        };
    }

    private static AvailableDataSource MakeRoiDataSource(RoiShape[] roi)
    {
        return new AvailableDataSource
        {
            SourceNodeId = "Node1",
            SourceNodeName = "找圆工具",
            PropertyName = "检测区域",
            DisplayName = "检测区域",
            PropertyType = typeof(RoiShape[]),
            VariableType = "ROI",
            CurrentValue = roi,
        };
    }

    [Fact]
    public void BuildRoiTypedChildren_RoiTarget_NoPerElementNodes_NoFieldAggregation()
    {
        // 有运行值时，ROI 目标的绑定树仍为声明式类型树（整体 + 各类型集合叶）：
        // 不含 [0]~[4] 逐元素节点、不含 "--" 字段聚合错位（运行/设计期结构一致）。
        var roi = MakeHeterogeneousRoi();
        var ds = MakeRoiDataSource(roi);

        var children = ArrayTreeFormatter.BuildRoiTypedChildren(ds, "ROI");

        // 整体 + 全部形状叶（声明式全枚举，与 roi 实际包含哪几类无关；
        // 形状数按枚举推导——ShapeType.Rectangle 已并入 RotatedRectangle，硬编码会随枚举演进失效）
        Assert.Equal(System.Enum.GetValues<ShapeType>().Count(t => t != ShapeType.None) + 1, children.Count);

        // "整体"入口：绑定整个检测区域数组（BindingSuffix=null → 路径 "[]"）
        var overall = children[0];
        Assert.Equal("整体", overall.Text);
        Assert.True(overall.IsSelectable);
        Assert.NotNull(overall.DataSource);
        Assert.Null(overall.DataSource!.BindingSuffix);
        Assert.Equal("Node1.检测区域[]", overall.DataSource.GetBindingPath());
        Assert.Empty(overall.Children);

        // 类型集合叶：圆/多边形/旋转矩形均可绑 [Type=X]，无 children（绑定终点=叶子）
        var circle = children.First(c => c.Text == "圆");
        Assert.True(circle.IsSelectable);
        Assert.NotNull(circle.DataSource);
        Assert.Equal("[Type=Circle]", circle.DataSource!.BindingSuffix);
        Assert.Equal("Node1.检测区域[Type=Circle]", circle.DataSource.GetBindingPath());
        Assert.Empty(circle.Children);

        var polygon = children.First(c => c.Text == "多边形");
        Assert.Equal("[Type=Polygon]", polygon.DataSource!.BindingSuffix);
        var rr = children.First(c => c.Text == "旋转矩形");
        Assert.Equal("[Type=RotatedRectangle]", rr.DataSource!.BindingSuffix);

        // 不再展开逐元素 [i]（绑定第 N 个元素走文本手动路径，不在树中铺）
        Assert.DoesNotContain(children, c => c.Text.StartsWith("["));
        Assert.All(children, c => Assert.DoesNotContain("--", c.Text));
    }

    [Fact]
    public void GetShapeTypeShortName_MapsToDisplayName()
    {
        Assert.Equal("圆", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Circle));
        // 注：ShapeType.Rectangle 已删除（P0-2 统一为 RotatedRectangle），此处只断言旋转矩形
        Assert.Equal("旋转矩形", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.RotatedRectangle));
        Assert.Equal("多边形", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Polygon));
        Assert.Equal("圆环", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Annulus));
        Assert.Equal("弧形", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Arc));
    }

    #region OutputVariableResolver [Type=] 过滤（SSOT，rule-015 报错的合法类型/非法类型）

    private class FakeResults : ToolResults
    {
        private readonly IReadOnlyList<OutputVariable> _vars;
        public FakeResults(IReadOnlyList<OutputVariable> vars) => _vars = vars;
        public override IReadOnlyList<OutputVariable> GetOutputVariables() => _vars;
    }

    private static IDictionary<string, ToolResults> MakeResults(RoiShape[] roi)
    {
        var variables = new List<OutputVariable>
        {
            new OutputVariable
            {
                Name = "检测区域",
                VariableType = "ROI",
                DisplayName = "检测区域",
                RuntimeValue = roi,
            }
        };
        return new Dictionary<string, ToolResults> { ["Node1"] = new FakeResults(variables) };
    }

    [Fact]
    public void Resolve_TypeFilter_ReturnsOnlyCircle()
    {
        var results = MakeResults(MakeHeterogeneousRoi());

        var resolved = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Circle]");
        var arr = Assert.IsType<RoiShape[]>(resolved);
        Assert.Equal(2, arr.Length);
        Assert.All(arr, sp => Assert.Equal(ShapeType.Circle, sp.ShapeType));
    }

    [Fact]
    public void Resolve_NoFilter_ReturnsAll()
    {
        var results = MakeResults(MakeHeterogeneousRoi());

        var resolved = OutputVariableResolver.Resolve(results, "Node1", "检测区域");
        var arr = Assert.IsType<RoiShape[]>(resolved);
        Assert.Equal(5, arr.Length);
    }

    [Fact]
    public void Resolve_OverallArrayBracket_ReturnsAll()
    {
        // "整体"入口路径 "检测区域[]"（BindingSuffix=null 的显式 []）：
        // 运行期保留整个数组，返回全部图形（整体入口）。
        var results = MakeResults(MakeHeterogeneousRoi());

        var resolved = OutputVariableResolver.Resolve(results, "Node1", "检测区域[]");
        var arr = Assert.IsType<RoiShape[]>(resolved);
        Assert.Equal(5, arr.Length);
    }

    [Fact]
    public void Resolve_TypeFilter_NoMatch_Throws()
    {
        var results = MakeResults(MakeHeterogeneousRoi());
        Assert.Throws<InvalidOperationException>(() =>
            OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Rectangle]"));
    }

    [Fact]
    public void Resolve_TypeFilter_InvalidType_Throws()
    {
        var results = MakeResults(MakeHeterogeneousRoi());
        Assert.Throws<InvalidOperationException>(() =>
            OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Invalid]"));
    }

    [Fact]
    public void Resolve_TypeFilterThenIndex_ReturnsNthOfType_InRange()
    {
        // [Type=Circle][1] 取第 1 个圆（索引越界判断在过滤后数组上进行）
        var results = MakeResults(MakeHeterogeneousRoi());
        var resolved = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Circle][1]");
        var circle = Assert.IsType<RoiShape>(resolved);
        Assert.Equal(ShapeType.Circle, circle.ShapeType);
    }

    [Fact]
    public void Resolve_FieldPath_NoIndex_ProjectsAllOfType()
    {
        // 数值目标字段叶默认路径 [Type=X][].Geometry.字段（数组集合语义，不自动填索引）：
        // 绑定"该类型全部图形的该字段"，逐元素投影返回 double[]。
        var results = MakeResults(MakeHeterogeneousRoi());

        var radii = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Circle][].Geometry.Radius");
        var arr = Assert.IsType<double[]>(radii);
        Assert.Equal(2, arr.Length); // 两个圆 → 半径集合 {50, 30}
        Assert.Equal(50.0, arr[0]);
        Assert.Equal(30.0, arr[1]);

        var widths = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=RotatedRectangle][].Geometry.Width");
        var widthArr = Assert.IsType<double[]>(widths);
        Assert.Equal(2, widthArr.Length);
        Assert.All(widthArr, w => Assert.Equal(30.0, w));
    }

    [Fact]
    public void Resolve_FieldPath_NoIndex_SingleShape_StillReturnsArray()
    {
        // 1 颗该类型图形时，无索引字段叶仍返回长度 1 数组（double[1]）而非标量——
        // 保证"有几个解析几个"向量化拼装恒走数组路径（N=1 与 N>1 形态一致）。
        var results = MakeResults(new[] { new RoiShape { Geometry = new Circle2d(100, 80, 50) } });

        var radii = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Circle][].Geometry.Radius");
        var arr = Assert.IsType<double[]>(radii);
        Assert.Single(arr);
        Assert.Equal(50.0, arr[0]);
    }

    [Fact]
    public void Resolve_FieldPath_ManualIndex_TakesNthOfType()
    {
        // 用户手写索引 [Type=X][0]：仍取该类型第 0 个元素的几何字段（兼容手动精确指定）
        var results = MakeResults(MakeHeterogeneousRoi());

        var radius = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Circle][0].Geometry.Radius");
        Assert.Equal(50.0, Assert.IsType<double>(radius));

        var width = OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=RotatedRectangle][1].Geometry.Width");
        Assert.Equal(30.0, Assert.IsType<double>(width));
    }

    [Fact]
    public void Resolve_FieldPath_TypeNotPresent_Throws()
    {
        // 运行期检测区无该类型（如无弧形）→ 直接报错（rule-015），不静默回退
        var results = MakeResults(MakeHeterogeneousRoi());
        Assert.ThrowsAny<System.Exception>(() =>
            OutputVariableResolver.Resolve(results, "Node1", "检测区域[Type=Arc][0].Geometry.Radius"));
    }

    [Fact]
    public void ParsePropertyPart_DoubleBracket_TypePlusIndex()
    {
        // 双括号 [Type=X][n] → 同时返回类型过滤 + 索引
        var (name, index, typeFilter) = PropertyPathParser.ParsePropertyPart("检测区域[Type=Circle][2]");
        Assert.Equal("检测区域", name);
        Assert.Equal(2, index);
        Assert.Equal("Circle", typeFilter);

        // 仅 [Type=X]
        var (name2, index2, typeFilter2) = PropertyPathParser.ParsePropertyPart("检测区域[Type=Circle]");
        Assert.Equal("检测区域", name2);
        Assert.Null(index2);
        Assert.Equal("Circle", typeFilter2);

        // 仅 [n]
        var (name3, index3, typeFilter3) = PropertyPathParser.ParsePropertyPart("检测区域[0]");
        Assert.Equal("检测区域", name3);
        Assert.Equal(0, index3);
        Assert.Null(typeFilter3);
    }

    #endregion
}
