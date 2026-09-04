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
        // 圆弧/箭头/坐标系渲染不支持，不过滤（null = 显示全部，保持遗留行为）
        Assert.Null(new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Arc }.ParamType);
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
    public void TextDisplayItem_Filter_OnlyNumericSources()
    {
        // 文本显示项默认绑数字（Double），Integer↔Double 互通，字符串结果被过滤
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Text };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Single(filtered);
        Assert.Equal("Value", filtered[0].PropertyName);
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
        // 圆弧（ParamType=null）不过滤，列表包含全部数据源（遗留行为）
        var item = new SubroutineDisplayItem { DisplayType = SubroutineDisplayType.Arc };
        var filtered = ParamBindingBase.FilterDataSourcesByType(MakeAllSources(), item.ParamType!);
        Assert.Equal(MakeAllSources().Count, filtered.Count);
    }

    #endregion
}
