using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>ROI 组的语义基线：模式解析、继承物化、屏蔽区继承和多边形往返。</summary>
public sealed class BlobRoiBaselineTests
{
    private static InputValues Inputs(IReadOnlyDictionary<string, object?> values, params Port[] extra)
        => new(BlobRoiInheritance.Ports.Concat(extra), values);

    private static Dictionary<string, object?> RectangleLeaves(double centerX, double centerY, double width, double height, double angle)
        => new(StringComparer.Ordinal)
        {
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, "center-x")] = new[] { centerX },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, "center-y")] = new[] { centerY },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, "width")] = new[] { width },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, "height")] = new[] { height },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, "angle-degrees")] = new[] { angle }
        };

    [Fact]
    public void Local_mode_returns_the_existing_local_roi_unchanged()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobRoiInheritance.ModeId] = BlobRoiInheritance.Local
        };

        var result = BlobRoiInheritance.Materialize(Inputs(values), BlobLocalRoi.Empty);

        Assert.Same(BlobLocalRoi.Empty, result);
    }

    [Fact]
    public void Unset_mode_is_treated_as_local_drawing()
    {
        var result = BlobRoiInheritance.Materialize(Inputs(new Dictionary<string, object?>()), BlobLocalRoi.Empty);

        Assert.Same(BlobLocalRoi.Empty, result);
    }

    [Fact]
    public void Rectangle_parameter_inheritance_produces_one_rectangle_shape()
    {
        var values = RectangleLeaves(100, 200, 50, 30, 15);
        values[BlobRoiInheritance.ModeId] = BlobRoiInheritance.RectangleParameters;

        var result = BlobRoiInheritance.Materialize(Inputs(values), null);

        Assert.NotNull(result);
        var items = BlobLocalRoi.Items(result!, BlobLocalRoi.Inspection);
        var item = Assert.Single(items);
        var shape = item.Get<RecordValue>("shape");
        Assert.Equal("rectangle", shape.Get<string>("kind"));
        Assert.Equal(100d, shape.Get<RecordValue>("center").Get<double>("x"), 3);
        Assert.Equal(200d, shape.Get<RecordValue>("center").Get<double>("y"), 3);
        Assert.Equal(50d, shape.Get<double>("width"), 3);
        Assert.Equal(30d, shape.Get<double>("height"), 3);
        Assert.Equal(BlobLocalRoi.Explicit, BlobLocalRoi.Coverage(result!, BlobLocalRoi.Inspection));
    }

    [Fact]
    public void Annulus_parameter_inheritance_keeps_inner_and_outer_radius_and_sweep()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobRoiInheritance.ModeId] = BlobRoiInheritance.AnnulusParameters,
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "center-x")] = new[] { 50d },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "center-y")] = new[] { 60d },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "inner-radius")] = new[] { 10d },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "outer-radius")] = new[] { 40d },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "start-degrees")] = new[] { 30d },
            [BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, "sweep-degrees")] = new[] { 90d }
        };

        var result = BlobRoiInheritance.Materialize(Inputs(values), null);

        Assert.NotNull(result);
        var shape = Assert.Single(BlobLocalRoi.Items(result!, BlobLocalRoi.Inspection)).Get<RecordValue>("shape");
        Assert.Equal("annulus", shape.Get<string>("kind"));
        Assert.Equal(10d, shape.Get<double>("inner-radius"), 3);
        Assert.Equal(40d, shape.Get<double>("outer-radius"), 3);
        Assert.Equal(Math.PI / 6, shape.Get<double>("start-radians"), 6);
        Assert.Equal(Math.PI / 6 + Math.PI / 2, shape.Get<double>("end-radians"), 6);
    }

    [Fact]
    public void Mask_inheritance_switch_rejects_non_constant_values()
    {
        Assert.True(BlobMaskInheritance.IsInherited(new ConstantSetting(true)));
        Assert.False(BlobMaskInheritance.IsInherited(new ConstantSetting(false)));
        Assert.False(BlobMaskInheritance.IsInherited(new UnsetSetting()));
        Assert.Throws<ArgumentException>(() => BlobMaskInheritance.IsInherited(new ConstantSetting("true")));
    }

    [Fact]
    public void Inherited_mask_polygon_round_trips_its_vertices()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobMaskInheritance.ModeId] = true,
            [BlobMaskInheritance.SourceId] = new SequenceValue(BlobMaskInheritance.PolygonStrings,
                new object?[] { "0,0,10,0,10,10" })
        };

        var result = BlobMaskInheritance.Materialize(new InputValues(BlobMaskInheritance.Ports, values), BlobLocalRoi.Empty);

        Assert.NotNull(result);
        var maskItems = BlobLocalRoi.Items(result!, BlobLocalRoi.Mask);
        var shape = Assert.Single(maskItems).Get<RecordValue>("shape");
        Assert.Equal("polygon", shape.Get<string>("kind"));
        var vertices = shape.Get<SequenceValue>("vertices").Cast<RecordValue>().ToArray();
        Assert.Equal(3, vertices.Length);
        Assert.Equal(0d, vertices[0].Get<double>("x"), 3);
        Assert.Equal(10d, vertices[2].Get<double>("y"), 3);
        Assert.Equal(BlobLocalRoi.Explicit, BlobLocalRoi.Coverage(result!, BlobLocalRoi.Mask));
    }

    [Fact]
    public void Disabled_mask_inheritance_keeps_the_local_mask_group()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BlobMaskInheritance.ModeId] = false
        };

        var result = BlobMaskInheritance.Materialize(new InputValues(BlobMaskInheritance.Ports, values), BlobLocalRoi.Empty);

        Assert.Same(BlobLocalRoi.Empty, result);
    }
}
