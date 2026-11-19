using SunEyeVision.Plugin.Contracts;
using Xunit;

namespace SunEyeVision.Tool.BlobAnalysis.Tests;

/// <summary>平台 ROI 文档契约：结构命名中性、构造往返、校验与相等语义。</summary>
public sealed class RoiDocumentValueTests
{
    private static RecordValue Rectangle(double centerX = 10, double centerY = 20, double width = 40, double height = 20)
        => new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "rectangle",
            ["center"] = RoiDocumentValue.Point(centerX, centerY),
            ["width"] = width,
            ["height"] = height,
            ["angle-degrees"] = 0d
        });

    private static RecordValue Polygon() => new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
    {
        ["kind"] = "polygon",
        ["vertices"] = new SequenceValue(TypeRef.SequenceOf(RoiDocumentValue.PointType), new object?[]
        {
            RoiDocumentValue.Point(0, 0), RoiDocumentValue.Point(10, 0), RoiDocumentValue.Point(10, 10)
        })
    });

    [Fact]
    public void Declared_record_identities_are_neutral_and_distinct()
    {
        Assert.Equal("roi.document", RoiDocumentValue.Declaration.RecordId);
        Assert.Equal("roi.group", RoiDocumentValue.GroupType.RecordId);
        Assert.Equal("roi.item", RoiDocumentValue.ItemType.RecordId);
        Assert.Equal("roi.shape", RoiDocumentValue.ShapeType.RecordId);
        Assert.Equal("roi.point", RoiDocumentValue.PointType.RecordId);
    }

    [Fact]
    public void Empty_is_a_shared_singleton_with_a_valid_structure()
    {
        Assert.Same(RoiDocumentValue.Empty, RoiDocumentValue.Empty);
        RoiDocumentValue.Validate(RoiDocumentValue.Empty);
        Assert.Equal(RoiDocumentValue.FullDomain, RoiDocumentValue.Coverage(RoiDocumentValue.Empty, RoiDocumentValue.Inspection));
        Assert.Equal(RoiDocumentValue.None, RoiDocumentValue.Coverage(RoiDocumentValue.Empty, RoiDocumentValue.Mask));
    }

    [Fact]
    public void Explicit_group_roundtrips_through_items_and_coverage()
    {
        var id = Guid.NewGuid();
        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(id, "检测区域1", Rectangle()) }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        RoiDocumentValue.Validate(root);
        var items = RoiDocumentValue.Items(root, RoiDocumentValue.Inspection);
        var item = Assert.Single(items);
        Assert.Equal(id.ToString("N"), item.Get<string>("id"));
        Assert.Equal(RoiDocumentValue.Explicit, RoiDocumentValue.Coverage(root, RoiDocumentValue.Inspection));
    }

    [Fact]
    public void Coverage_without_items_is_rejected()
    {
        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, Array.Empty<RecordValue>()),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        Assert.Throws<ArgumentException>(() => RoiDocumentValue.Validate(root));
    }

    [Fact]
    public void Mask_group_rejects_non_polygon_shapes()
    {
        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.FullDomain, Array.Empty<RecordValue>()),
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(Guid.NewGuid(), "屏蔽区", Rectangle()) }));

        Assert.Throws<ArgumentException>(() => RoiDocumentValue.Validate(root));
    }

    [Fact]
    public void Duplicate_item_identities_are_rejected()
    {
        var id = Guid.NewGuid();
        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit, new[]
            {
                RoiDocumentValue.Item(id, "区域1", Rectangle()),
                RoiDocumentValue.Item(id, "区域2", Rectangle())
            }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        Assert.Throws<ArgumentException>(() => RoiDocumentValue.Validate(root));
    }

    [Fact]
    public void Polygon_shape_is_accepted_in_the_mask_group()
    {
        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.FullDomain, Array.Empty<RecordValue>()),
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(Guid.NewGuid(), "屏蔽区", Polygon()) }));

        RoiDocumentValue.Validate(root);
        Assert.Single(RoiDocumentValue.Items(root, RoiDocumentValue.Mask));
    }

    [Fact]
    public void Same_compares_documents_by_structure()
    {
        Assert.True(RoiDocumentValue.Same(RoiDocumentValue.Empty, RoiDocumentValue.Empty));

        var root = RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(Guid.NewGuid(), "检测区域1", Rectangle()) }),
            RoiDocumentValue.Group(RoiDocumentValue.None, Array.Empty<RecordValue>()));

        Assert.True(RoiDocumentValue.Same(root, root));
        Assert.False(RoiDocumentValue.Same(root, RoiDocumentValue.Empty));
    }
}
