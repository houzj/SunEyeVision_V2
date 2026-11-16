using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>
/// 「模块结果树」显示声明与原厂截图逐项对齐（docs/collaboration/evidence/Blob模块结果树原件结构-20261112.json）。
/// 项名、顺序、分组与成员路径都来自 BlobOutputDisplay；改动必须同步证据 JSON。
/// </summary>
public sealed class BlobResultTreeTests
{
    private static IReadOnlyList<OutputDisplayNode> Display => BlobSchema.OutputDisplay;
    private static OutputDisplayNode Visible(string name) => Display.Single(item => item.DisplayInResultPanel && item.DisplayName == name);

    [Fact]
    public void Display_declaration_is_valid_against_the_declared_outputs()
    {
        var schema = new ToolSchema(Array.Empty<Port>(), BlobSchema.Outputs, outputDisplay: BlobSchema.OutputDisplay);
        Assert.Equal(BlobSchema.OutputDisplay.Select(item => item.FieldId), schema.OutputDisplay.Select(item => item.FieldId));
        Assert.Equal("耗时（ms）", schema.FindOutputDisplay(BlobSchema.ModuleRunTimePort)!.DisplayName);
    }

    [Fact]
    public void Visible_items_follow_the_original_module_order()
    {
        var expected = new[]
        {
            "耗时（ms）","模块状态","单体状态","Blob个数","面积","总面积","质心点","最小外接矩形",
            "最小灰度值","最大灰度值","对比度","最大内接矩形","Blob矩形框","周长","长轴","短轴","圆形度","矩形度",
            "二值化图像数据","Blob图像数据","检测区域","ROI圆弧","轮廓点集","屏蔽区多边形","输出掩膜"
        };
        Assert.Equal(expected, Display.Where(item => item.DisplayInResultPanel).Select(item => item.DisplayName).ToArray());
        // 原件「分数」在本方没有 Blob 级 score 数据来源（只有轮廓判定值 0/2），按禁止兜底原则不声明该行。
        Assert.DoesNotContain(Display, item => item.DisplayName == "分数");
    }

    [Fact]
    public void Evidence_outside_items_stay_hidden_without_touching_the_schema()
    {
        foreach(var (portId, name) in new (string, string)[]
        {
            (BlobSchema.Field(8),"低阈值"),(BlobSchema.Field(9),"高阈值"),(BlobSchema.Field(10),"极性"),(BlobSchema.Field(102),"阈值方式"),
            (BlobSchema.Field(13),"平均面积"),(BlobSchema.Field(14),"平均圆度"),(BlobSchema.Field(15),"平均矩形度"),(BlobSchema.Field(16),"白色像素比例"),
            (BlobSchema.Field(17),"灰度直方图"),(BlobSchema.Field(18),"通道均值"),(BlobSchema.Field(27),"矩形尺寸"),
            (BlobSchema.Field(30),"最小面积"),(BlobSchema.Field(31),"最大面积"),
            (BlobSchema.BlobsPort,"Blob明细"),(BlobSchema.RegionPort,"检测结果区域")
        })
        {
            var row = Display.Single(item => item.FieldId == portId);
            Assert.Equal(name, row.DisplayName);
            Assert.False(row.DisplayInResultPanel);
        }
    }

    [Fact]
    public void Transposed_groups_declare_member_rows_instead_of_element_indices()
    {
        var centroid = Visible("质心点");
        Assert.Equal(new[] { "x", "y" }, centroid.Children.Select(item => item.FieldId).ToArray());
        Assert.Equal(new[] { "质心X", "质心Y" }, centroid.Children.Select(item => item.DisplayName).ToArray());

        var outer = Visible("最小外接矩形");
        Assert.Equal(new[] { "center", "width", "height", "angle-degrees" }, outer.Children.Select(item => item.FieldId).ToArray());
        Assert.Equal("中心点", outer.Children[0].DisplayName);
        Assert.Equal(new[] { "x", "y" }, outer.Children[0].Children.Select(item => item.FieldId).ToArray());
        Assert.Equal(new[] { "矩形宽度", "矩形高度", "角度" }, outer.Children.Skip(1).Select(item => item.DisplayName).ToArray());

        var inner = Visible("最大内接矩形");
        Assert.Equal(new[] { "最大内接矩形中心X", "最大内接矩形中心Y" }, inner.Children[0].Children.Select(item => item.DisplayName).ToArray());
        Assert.Equal(new[] { "最大内接矩形宽度", "最大内接矩形高度", "最大内接矩形角度" }, inner.Children.Skip(1).Select(item => item.DisplayName).ToArray());
    }

    [Fact]
    public void Bounding_box_group_spans_declared_ports()
    {
        var bounds = Visible("Blob矩形框");
        Assert.Equal(new[]
        {
            "origin","width","height","top-left-x","top-left-y","bottom-right-x","bottom-right-y","principal-angle"
        }, bounds.Children.Select(item => item.FieldId).ToArray());
        Assert.Equal(new[] { "Blob矩形框原点X", "Blob矩形框原点Y" }, bounds.Children[0].Children.Select(item => item.DisplayName).ToArray());
        Assert.Equal(BlobSchema.RectangleTopLeftXPort, bounds.Children[3].PortId);
        Assert.Equal(BlobSchema.RectangleTopLeftYPort, bounds.Children[4].PortId);
        Assert.Equal(BlobSchema.RectangleBottomRightXPort, bounds.Children[5].PortId);
        Assert.Equal(BlobSchema.RectangleBottomRightYPort, bounds.Children[6].PortId);
        Assert.Equal(BlobSchema.PrincipalAnglesPort, bounds.Children[7].PortId);
    }

    [Fact]
    public void Mask_group_projects_polygon_geometry_members()
    {
        var mask = Visible("屏蔽区多边形");
        Assert.Equal(new[] { "count", "vertex", "mask-strings" }, mask.Children.Select(item => item.FieldId).ToArray());
        Assert.Equal("多边形点数", mask.Children[0].DisplayName);
        var vertex = mask.Children[1];
        Assert.Equal("多边形点集", vertex.DisplayName);
        Assert.Equal(BlobSchema.MaskPolygonsPort, vertex.PortId);
        Assert.Equal(new[] { "多边形点X", "多边形点Y" }, vertex.Children.Select(item => item.DisplayName).ToArray());
        Assert.Equal(BlobSchema.MaskPolygonStringsPort, mask.Children[2].PortId);
    }

    [Fact]
    public void Image_groups_keep_their_declared_member_captions()
    {
        foreach(var (portId, expected) in new (string, string[])[]
        {
            (BlobSchema.Field(6), new[] { "二值化图像数据", "二值化图像数据", "二值化图像宽度", "二值化图像高度", "二值化图像像素格式" }),
            (BlobSchema.Field(5), new[] { "Blob图像数据", "Blob图像数据", "Blob图像宽度", "Blob图像高度", "Blob图像像素格式" }),
            (BlobSchema.MaskImagePort, new[] { "输出掩膜", "输出掩膜图像", "输出掩膜宽度", "输出掩膜高度", "输出掩膜像素格式" })
        })
        {
            var group = Display.Single(item => item.FieldId == portId);
            Assert.Equal(expected[0], group.DisplayName);
            Assert.Equal(expected.Skip(1).ToArray(), group.Children.Select(item => item.DisplayName).ToArray());
        }
        Assert.True(Display.Single(item => item.FieldId == BlobSchema.MaskImagePort).IsAuxiliaryImage);
    }

    [Fact]
    public void Renamed_items_match_the_evidence_captions()
    {
        Assert.Equal("对比度", Display.Single(item => item.FieldId == BlobSchema.GrayContrastsPort).DisplayName);
        Assert.Equal("检测区域", Visible("检测区域").DisplayName);
        Assert.Equal("ROI圆弧", Visible("ROI圆弧").DisplayName);
        Assert.Equal("轮廓点集", Visible("轮廓点集").DisplayName);
        Assert.Equal("主轴角度", Visible("Blob矩形框").Children[7].DisplayName);
        Assert.Equal("圆形度", Display.Single(item => item.FieldId == BlobSchema.Field(23)).DisplayName);
    }
}
