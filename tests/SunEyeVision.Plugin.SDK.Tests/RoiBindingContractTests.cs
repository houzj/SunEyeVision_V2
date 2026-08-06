using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// RoI 多图形统一绑定契约测试：绑定树按图形类型分组（无字段聚合错位）+ 运行期 [Type=] 过滤（SSOT）。
/// 对应方案验收标准 AC-01/AC-03/AC-04 与防御机制"契约测试 + 解析器单测"。
/// </summary>
public class RoiBindingContractTests
{
    private static ShapeParameters[] MakeHeterogeneousRoi()
    {
        return new ShapeParameters[]
        {
            new ShapeParameters { Geometry = new Circle2d(100, 80, 50) },
            new ShapeParameters { Geometry = new Circle2d(200, 80, 30) },
            new ShapeParameters { Geometry = new RotatedRect2d(10, 20, 30, 40, 15) },
            new ShapeParameters { Geometry = new RotatedRect2d(50, 60, 30, 40, 15) },
            new ShapeParameters { Geometry = new Polygon2d(new[] { new System.Windows.Point(0, 0), new System.Windows.Point(10, 0), new System.Windows.Point(5, 10) }) },
        };
    }

    private static AvailableDataSource MakeRoiDataSource(ShapeParameters[] roi)
    {
        return new AvailableDataSource
        {
            SourceNodeId = "Node1",
            SourceNodeName = "找圆工具",
            PropertyName = "检测区域",
            DisplayName = "检测区域",
            PropertyType = typeof(ShapeParameters[]),
            VariableType = "RoI",
            CurrentValue = roi,
        };
    }

    [Fact]
    public void BuildRoiBindingChildren_GroupsByShapeType_NoFieldAggregation()
    {
        var roi = MakeHeterogeneousRoi();
        var ds = MakeRoiDataSource(roi);

        var children = ArrayTreeFormatter.BuildRoiBindingChildren(ds, roi);

        // 3 个分组 + 5 个单元素 = 8 子节点
        Assert.Equal(8, children.Count);

        // 分组节点（按 ShapeType 排序：Circle, Polygon, RotatedRectangle）
        var circleGroup = children.First(c => c.Text.Contains("圆 (2项)"));
        Assert.True(circleGroup.IsSelectable);
        Assert.NotNull(circleGroup.DataSource);
        Assert.Equal("[Type=Circle]", circleGroup.DataSource!.BindingSuffix);
        Assert.Equal("Node1.检测区域[Type=Circle]", circleGroup.DataSource.GetBindingPath());

        var polygonGroup = children.First(c => c.Text.Contains("多边形 (1项)"));
        Assert.Equal("[Type=Polygon]", polygonGroup.DataSource!.BindingSuffix);

        var rrGroup = children.First(c => c.Text.Contains("旋转矩形 (2项)"));
        Assert.Equal("[Type=RotatedRectangle]", rrGroup.DataSource!.BindingSuffix);

        // 单元素节点 [0]..[4] 均可选
        for (int i = 0; i < 5; i++)
        {
            var elem = children.First(c => c.Text.StartsWith($"[{i}]"));
            Assert.True(elem.IsSelectable);
            Assert.Equal($"[{i}]", elem.DataSource!.BindingSuffix);
            Assert.Equal($"Node1.检测区域[{i}]", elem.DataSource.GetBindingPath());
        }

        // 无字段聚合错位（原 "-" 错位是异质数组的 bug）
        Assert.All(children, c => Assert.DoesNotContain("--", c.Text));
    }

    [Fact]
    public void GetShapeTypeShortName_MapsToDisplayName()
    {
        Assert.Equal("圆", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Circle));
        Assert.Equal("矩形", ArrayTreeFormatter.GetShapeTypeShortName(ShapeType.Rectangle));
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

    private static IDictionary<string, ToolResults> MakeResults(ShapeParameters[] roi)
    {
        var variables = new List<OutputVariable>
        {
            new OutputVariable
            {
                Name = "检测区域",
                VariableType = "RoI",
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
        var arr = Assert.IsType<ShapeParameters[]>(resolved);
        Assert.Equal(2, arr.Length);
        Assert.All(arr, sp => Assert.Equal(ShapeType.Circle, sp.ShapeType));
    }

    [Fact]
    public void Resolve_NoFilter_ReturnsAll()
    {
        var results = MakeResults(MakeHeterogeneousRoi());

        var resolved = OutputVariableResolver.Resolve(results, "Node1", "检测区域");
        var arr = Assert.IsType<ShapeParameters[]>(resolved);
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

    #endregion
}
