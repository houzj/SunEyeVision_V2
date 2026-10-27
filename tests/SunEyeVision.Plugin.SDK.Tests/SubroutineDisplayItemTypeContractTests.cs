using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 契约测试：子程序显示项 ParamType 必须按 DisplayType 映射到对应数据源类型，
/// 使绑定下拉（ParamBindingBase.FilterDataSourcesByType）能按显示类型过滤数据源，
/// 避免"加圆显示项却列出所有类型数据源"。
/// </summary>
public class SubroutineDisplayItemTypeContractTests
{
    private static AvailableDataSource MakeDs(string name, Type propertyType)
        => new()
        {
            SourceNodeId = "N1",
            SourceNodeName = "上游节点",
            PropertyName = name,
            DisplayName = name,
            PropertyType = propertyType,
        };

    private static List<AvailableDataSource> MakeAllSources()
        => new()
        {
            MakeDs("Image", typeof(IVisionImage)),
            MakeDs("Circle", typeof(Circle2d[])),
            MakeDs("Point", typeof(Point2d[])),
            MakeDs("Line", typeof(Line2d[])),
            MakeDs("Polygon", typeof(Polygon2d[])),
            MakeDs("Rect", typeof(RotatedRect2d[])),
            MakeDs("Value", typeof(double[])),
            MakeDs("Text", typeof(string[])),
        };

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(string[]), true)]
    [InlineData(typeof(bool), true)]
    [InlineData(typeof(bool[]), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(double[]), true)]
    [InlineData(typeof(IVisionImage), false)]
    [InlineData(typeof(Circle2d[]), false)]
    public void Formal_Group_text_binding_contract_accepts_declared_text_boolean_numeric_values_and_rejects_geometry_image(Type type, bool accepted)
    {
        var p = new SubroutineParameters { SubroutineId="child" };
        var display = new SubroutineDisplayItem { DisplayName="文本", DisplayType=SubroutineDisplayType.Text, TextStyle=new() };
        p.DisplayConfigs.Add(display);
        var input = BindingInputCatalog.Describe(p).Single(i => i.Key=="Display:"+display.FieldId);
        var contract = new[] { new SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable
        { Name="Value", DeclaredType=type, VariableType=SdkTypeVocabulary.ToTypeVocabulary(type)!, IsCollection=type.IsArray } };
        var result = BindingContractValidator.Validate(contract,type.IsArray?"Value[]":"Value",input.AllowedTypes);
        Assert.Equal(accepted,result.State is SunEyeVision.Plugin.Contracts.BindingCompatibility.Compatible or SunEyeVision.Plugin.Contracts.BindingCompatibility.Conditional);
        Assert.True(input.InternalProjection); Assert.Equal("child",input.ReadWorkflowId!());
    }

    #region 契约：ParamType 类型映射

    [Fact]
    public void ParamType_MapsEachDisplayType()
    {
        Assert.Equal(typeof(Circle2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Circle }.ParamType);
        Assert.Equal(typeof(Point2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Point }.ParamType);
        Assert.Equal(typeof(Point2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Cross }.ParamType);
        Assert.Equal(typeof(Line2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Line }.ParamType);
        Assert.Equal(typeof(Polygon2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Polygon }.ParamType);
        Assert.Equal(typeof(Polygon2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Contour }.ParamType);
        Assert.Equal(typeof(RotatedRect2d[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Rectangle }.ParamType);
        Assert.Equal(typeof(IVisionImage), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Image }.ParamType);
        Assert.Equal(typeof(double[]), new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Text }.ParamType);
    }

    [Fact]
    public void ParamType_UnsupportedDisplayType_IsNull()
    {
        // 箭头/坐标系渲染不支持，不过滤（null = 显示全部，保持遗留行为）
        Assert.Equal(typeof(Arc2d[]),new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Arc }.ParamType);
        Assert.Null(new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Arrow }.ParamType);
        Assert.Null(new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.CoordinateSystem }.ParamType);
    }

    [Fact]
    public void DisplayTypeChange_NotifiesParamType()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Circle };
        var notified = false;
        item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(item.ParamType)) notified = true; };
        item.DisplayType = SubroutineDisplayType.Point;
        Assert.True(notified);
    }

    #endregion

    #region 契约：绑定下拉按显示类型过滤

    [Fact]
    public void CircleDisplayItem_Filter_OnlyCircleSources()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Circle };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Single(filtered);
        Assert.Equal("Circle", filtered[0].PropertyName);
    }

    [Fact]
    public void ImageDisplayItem_Filter_OnlyImageSources()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Image };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Single(filtered);
        Assert.Equal("Image", filtered[0].PropertyName);
    }

    [Fact]
    public void TextDisplayItem_Filter_TextBooleanNumericSourcesAndDrillableGeometry()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Text };
        var sources = MakeAllSources(); sources.Add(MakeDs("Flag", typeof(bool[])));
        var filtered = ParamBindingBase.FilterDataSourcesByTypes(sources, item.AllowedParamTypes!);

        var names = filtered.Select(d => d.PropertyName).ToList();

        Assert.Contains("Value", names);      // 纯数值源
        Assert.Contains("Circle", names);     // 可下钻 半径 / 中心X
        Assert.Contains("Point", names);      // 可下钻 X / Y
        Assert.Contains("Line", names);       // 可下钻 点X / 方向X
        Assert.Contains("Rect", names);       // 可下钻 中心X / 宽 / 高 / 角度
        Assert.DoesNotContain("Polygon", names); // 顶点数组无标量字段，放行会导致展开后无可绑叶
        Assert.DoesNotContain("Image", names);
        Assert.Contains("Text", names);
        Assert.Contains("Flag", names);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void RectangleDisplayItem_Filter_OnlyRotatedRectSources()
    {
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Rectangle };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Single(filtered);
        Assert.Equal("Rect", filtered[0].PropertyName);
    }

    [Fact]
    public void UnsupportedDisplayType_Filter_ShowsAllSources()
    {
        // 箭头尚无已声明源类型，暂不过滤；圆弧另验。
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Arrow };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Equal(MakeAllSources().Count, filtered.Count);
    }

    [Fact]
    public void ArcDisplayItem_filters_out_unrelated_sources()
    {
        var sources=MakeAllSources();sources.Add(new AvailableDataSource{SourceNodeId="arc",PropertyName="Arc",PropertyType=typeof(Arc2d[]),VariableType="Arc"});
        var item=new SubroutineDisplayItem{DisplayType=SubroutineDisplayType.Arc};
        var filtered=ParamBindingBase.FilterDataSourcesByType(sources,item.ParamType!);
        Assert.Equal("Arc",Assert.Single(filtered).PropertyName);
    }

    #endregion
}
