using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 几何数组"参数字段下钻"契约测试（对应按图形绑定优化的参数订阅补全）：
/// - <see cref="GeometryFieldSchema"/> 逐几何类型的字段声明（Polygon 顶点数组无标量字段）；
/// - <see cref="ParamBindingBase.FilterDataSourcesByType"/> 数值目标放行"有声明字段"的几何数组；
/// - <see cref="ArrayTreeFormatter.BuildGeometryFieldChildren"/> 生成字段叶，路径为 "属性.字段"（集合语义）；
/// - 数组节点展开策略（<see cref="ParamBindingBase"/> 的 ApplyArrayDisplay）：图形订阅（几何/ROI 目标）
///   不展开、数组节点即绑定终点；参数订阅（数值目标）保留 [i] 只读预览；
/// - <see cref="OutputVariableResolver"/> 对 "属性.字段" 的解析：N=1 解包为标量，N&gt;1 逐元素投影为数组。
/// </summary>
public class GeometryFieldSchemaContractTests
{
    [Theory]
    [InlineData(typeof(Circle2d), 3)]
    [InlineData(typeof(Line2d), 4)]
    [InlineData(typeof(Point2d), 2)]
    [InlineData(typeof(RotatedRect2d), 5)]
    [InlineData(typeof(Arc2d), 5)]
    [InlineData(typeof(Annulus2d), 6)]
    public void GetFields_DeclaredGeometry_ReturnsExpectedFieldCount(Type geometryType, int expectedCount)
    {
        Assert.True(GeometryFieldSchema.HasFields(geometryType));
        Assert.Equal(expectedCount, GeometryFieldSchema.GetFields(geometryType).Count);
    }

    [Fact]
    public void GetFields_Polygon2d_HasNoScalarField()
    {
        // 顶点数组不可作为标量字段下钻（与 ROI 的 Polygon 处理一致：仅整体可绑）
        Assert.False(GeometryFieldSchema.HasFields(typeof(Polygon2d)));
        Assert.Empty(GeometryFieldSchema.GetFields(typeof(Polygon2d)));
    }

    [Fact]
    public void FilterDataSourcesByType_NumericTarget_SeesGeometryArrayWithFields()
    {
        // 数值参数（double）需要看到圆数组，才能下钻到"半径/圆心X"
        var sources = new List<AvailableDataSource> { MakeDataSource("Circle", typeof(Circle2d[])) };

        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(double));

        Assert.Contains(filtered, ds => ds.PropertyName == "Circle");
    }

    [Fact]
    public void FilterDataSourcesByType_NumericTarget_DoesNotSeeGeometryArrayWithoutFields()
    {
        // Polygon2d 无声明字段，放行会导致展开后无可绑叶 → 不放行
        var sources = new List<AvailableDataSource> { MakeDataSource("Contour", typeof(Polygon2d[])) };

        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(double));

        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "Contour");
    }

    [Fact]
    public void BuildGeometryFieldChildren_ProducesBindableLeavesWithScopedPath()
    {
        var ds = MakeDataSource("Circle", typeof(Circle2d[]));

        var children = ArrayTreeFormatter.BuildGeometryFieldChildren(ds, typeof(Circle2d));

        Assert.Equal(3, children.Count);
        Assert.All(children, c => Assert.True(c.IsSelectable, $"字段叶 '{c.Text}' 应可绑定"));

        var radius = children.Single(c => c.Text == "半径");
        // 树内路径与运行期绑定路径：属性名.字段（无 [Type=] 前缀，几何数组是同质的）
        Assert.Equal("Circle.Radius", radius.BindingPath);
        Assert.Equal("Node1.Circle.Radius[]", radius.DataSource!.GetBindingPath());
        Assert.True(radius.DataSource.IsArraySemantics);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Geometry_field_tree_binding_preserves_collection_for_zero_one_many_values(int count)
    {
        var source = MakeDataSource("Circle", typeof(Circle2d[]));
        var radius = ArrayTreeFormatter.BuildGeometryFieldChildren(source, typeof(Circle2d)).Single(c => c.Text == "半径");
        var path = radius.DataSource!.GetBindingPath();
        var result = new GeometryPointSourceResults { Circle = Enumerable.Range(0,count).Select(i => new Circle2d(i,i,i+2)).ToArray() };
        var resolved = OutputVariableResolver.Resolve(result, path[(path.IndexOf('.')+1)..]);
        Assert.Equal(Enumerable.Range(0,count).Select(i => (double)(i+2)), Assert.IsType<double[]>(resolved));
    }

    [Fact]
    public void Resolve_GeometryFieldPath_SingleElementUnwrapsToScalar()
    {
        var result = new GeometryPointSourceResults
        {
            Circle = new[] { new Circle2d(10, 20, 5) },
        };

        var value = OutputVariableResolver.Resolve(result, "Circle.Radius");

        // 长度 1 数组按契约解包为标量，兼容 ParamValue<double> 目标
        Assert.Equal(5.0, Assert.IsType<double>(value), 6);
    }

    [Fact]
    public void Resolve_GeometryFieldPath_MultipleElementsProjectsToArray()
    {
        var result = new GeometryPointSourceResults
        {
            Circle = new[] { new Circle2d(0, 0, 3), new Circle2d(0, 0, 7) },
        };

        var value = OutputVariableResolver.Resolve(result, "Circle.Radius");

        // 多元素逐元素投影（集合语义），取单个元素由用户在绑定框中手写 [i]
        Assert.Equal(new[] { 3.0, 7.0 }, Assert.IsType<double[]>(value));
    }

    [Fact]
    public void Resolve_GeometryFieldPath_ExplicitIndexSelectsSingleElement()
    {
        var result = new GeometryPointSourceResults
        {
            Circle = new[] { new Circle2d(0, 0, 3), new Circle2d(0, 0, 7) },
        };

        var value = OutputVariableResolver.Resolve(result, "Circle[1].Radius");

        Assert.Equal(7.0, Assert.IsType<double>(value), 6);
    }

    [Fact]
    public void BuildTreeStructure_GeometryTarget_ArrayNodeIsBindingEndpointWithNoChildren()
    {
        // 图形订阅场景（几何值目标）：数组节点即**绑定终点**，不展开逐元素 [i]。
        // 历史缺陷：几何数组节点挂上不可绑的 [i] 预览 → "点了先展开、展开里全是白点行"。
        var ds = MakeDataSource("Center", typeof(Point2d[]));
        ds.CurrentValue = new[] { new Point2d(1, 2) };

        var root = ParamBindingBase.BuildTreeStructure(
            new List<AvailableDataSource> { ds }, typeof(Point2d[])).Single();
        var node = root.Children.Single(c => c.Text == "Center[]");

        Assert.True(node.IsSelectable, "图形订阅下数组节点应可绑定（整组）");
        Assert.True(node.Children == null || node.Children.Count == 0,
            "图形订阅下数组节点不应展开（绑定终点必是叶子）");
    }

    [Fact]
    public void BuildTreeStructure_NumericTarget_PlainNumericArrayKeepsElementPreview()
    {
        // 参数订阅场景（数值目标）：保留逐元素 [i] **只读预览**（需要看值才能挑对源）。
        // [i] 不参与绑定；取第 N 个由用户编辑显示名（"Width[]" → "Width[2]"）。
        var ds = MakeDataSource("Width", typeof(double[]));
        ds.CurrentValue = new[] { 10.0, 20.0 };

        var root = ParamBindingBase.BuildTreeStructure(
            new List<AvailableDataSource> { ds }, typeof(double)).Single();
        var node = root.Children.Single(c => c.Text == "Width[]");

        Assert.NotEmpty(node.Children!);
        Assert.All(node.Children!, c => Assert.False(c.IsSelectable, "[i] 仅只读预览，不参与绑定"));
    }

    private static AvailableDataSource MakeDataSource(string propertyName, Type propertyType) => new()
    {
        SourceNodeId = "Node1",
        SourceNodeName = "圆查找",
        PropertyName = propertyName,
        DisplayName = propertyName,
        PropertyType = propertyType,
        VariableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(propertyType),
    };

    /// <summary>最小结果承载：仅声明 Circle2d[] 输出（几何字段路径解析用，不依赖具体工具）。</summary>
    private sealed class GeometryPointSourceResults : ToolResults
    {
        public Circle2d[]? Circle { get; set; }

        public override List<PropertyMapEntry> GetPropertyMap() =>
            [.. base.GetPropertyMap(), new("Circle", "拟合圆", "拟合结果")];
    }
}
