using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 参数绑定下拉类型过滤契约测试（方案：统一词汇表口径）。
/// 覆盖：标量/数组参数与上游单值/数组输出按词汇匹配、数字互通、
/// 不相关词汇（Image/String）不显示、未注册类型不显示。
/// 核心断言：标量参数能"看到"上游单值输出（修复形态学核宽度看不到图像载入节点的回归）。
/// </summary>
public class FilterDataSourcesByTypeTests
{
    private static AvailableDataSource MakeDs(string name, System.Type propertyType)
        => new()
        {
            SourceNodeId = "N1",
            SourceNodeName = "上游节点",
            PropertyName = name,
            DisplayName = name,
            PropertyType = propertyType,
        };

    private static List<AvailableDataSource> MakeSources(params AvailableDataSource[] sources)
        => new(sources);

    #region 契约：标量参数能看到上游单值/数组数值输出

    [Fact]
    public void ScalarInt_SeesUpstreamIntegerArrayOutput()
    {
        // 形态学核宽度(int) 绑 图像载入 Width(long[])：元数据对外提升为数组，必须可见
        var sources = MakeSources(MakeDs("Width", typeof(long[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.Contains(filtered, ds => ds.PropertyName == "Width");
    }

    [Fact]
    public void ScalarInt_SeesUpstreamIntArrayOutput()
    {
        var sources = MakeSources(MakeDs("Count", typeof(int[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.Contains(filtered, ds => ds.PropertyName == "Count");
    }

    [Fact]
    public void ScalarDouble_SeesUpstreamDoubleArrayOutput()
    {
        var sources = MakeSources(MakeDs("AspectRatio", typeof(double[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(double));
        Assert.Contains(filtered, ds => ds.PropertyName == "AspectRatio");
    }

    [Fact]
    public void ArrayParameter_SeesSameCandidateAsScalar()
    {
        // T 与 T[] 词汇归并：数组参数(int[]) 与 标量参数(int) 看到同一批候选
        var sources = MakeSources(MakeDs("Width", typeof(long[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int[]));
        Assert.Contains(filtered, ds => ds.PropertyName == "Width");
    }

    #endregion

    #region 契约：数字互通（Integer ↔ Double）

    [Fact]
    public void ScalarInt_SeesUpstreamDoubleOutput()
    {
        var sources = MakeSources(MakeDs("Area", typeof(double[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.Contains(filtered, ds => ds.PropertyName == "Area");
    }

    [Fact]
    public void ScalarDouble_SeesUpstreamIntegerOutput()
    {
        var sources = MakeSources(MakeDs("Count", typeof(long[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(double));
        Assert.Contains(filtered, ds => ds.PropertyName == "Count");
    }

    #endregion

    #region 契约：不相关词汇不显示（Image/String/RoI 对数值参数）

    [Fact]
    public void ScalarInt_DoesNotSeeUpstreamImageOutput()
    {
        var sources = MakeSources(MakeDs("OutputImage", typeof(IVisionImage)));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "OutputImage");
    }

    [Fact]
    public void ScalarInt_DoesNotSeeUpstreamStringOutput()
    {
        var sources = MakeSources(MakeDs("FilePath", typeof(string[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "FilePath");
    }

    [Fact]
    public void ScalarInt_DoesNotSeeUpstreamRoiOutput()
    {
        var sources = MakeSources(MakeDs("RoIs", typeof(ShapeParameters[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "RoIs");
    }

    #endregion

    #region 契约：RoI 参数按词汇匹配 / 未注册类型不显示

    [Fact]
    public void RoiParameter_SeesUpstreamRoiOutput()
    {
        var sources = MakeSources(MakeDs("Regions", typeof(ShapeParameters[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(ShapeParameters));
        Assert.Contains(filtered, ds => ds.PropertyName == "Regions");
    }

    [Fact]
    public void Scalar_UnregisteredDataSourceType_Hidden()
    {
        // 未映射词汇的类型（如 IVisionRegion，Region 已移出词汇表）→ 不显示
        var sources = MakeSources(MakeDs("Mask", typeof(IVisionRegion[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(int));
        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "Mask");
    }

    [Fact]
    public void NullTargetType_ReturnsAllSources()
    {
        var sources = MakeSources(MakeDs("A", typeof(long[])), MakeDs("B", typeof(IVisionImage)));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, null!);
        Assert.Equal(2, filtered.Count);
    }

    #endregion

    #region 契约：几何图形目标放行 RoI 容器数据源（Blob 检测区图形下钻）

    [Theory]
    [InlineData(typeof(Circle2d[]))]
    [InlineData(typeof(RotatedRect2d[]))]
    [InlineData(typeof(Line2d[]))]
    [InlineData(typeof(Polygon2d[]))]
    [InlineData(typeof(Point2d[]))]
    public void GeometryTarget_SeesRoiContainerSource(System.Type geometryTarget)
    {
        // 子程序"圆形/旋转矩形"等显示项 ParamType 是具体几何类型（词汇 Circle/RotatedRect...）。
        // Blob 检测区图形以 RoI（ShapeParameters[]）输出，内部按 [Type=X] 分组可下钻。
        // 目标词汇为几何图形时必须放行 RoI 容器数据源，否则绑定下拉里看不到 Blob 图形。
        var sources = MakeSources(MakeDs("检测区图形", typeof(ShapeParameters[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, geometryTarget);
        Assert.Contains(filtered, ds => ds.PropertyName == "检测区图形");
    }

    [Fact]
    public void ImageTarget_DoesNotSeeRoiContainerSource()
    {
        // 非几何图形目标（如 Image）不放行 RoI 容器数据源，避免错误绑定。
        var sources = MakeSources(MakeDs("检测区图形", typeof(ShapeParameters[])));
        var filtered = ParamBindingBase.FilterDataSourcesByType(sources, typeof(IVisionImage));
        Assert.DoesNotContain(filtered, ds => ds.PropertyName == "检测区图形");
    }

    #endregion
}
