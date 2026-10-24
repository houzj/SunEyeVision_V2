using System.Windows;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class DeclaredContourAddressTests
{
    private static Point[] Contour(double marker) => new[] { new Point(marker, 0), new Point(marker, 1) };
    private static DeclaredElementAddress Address(long id, params string[] path) => new("blobs", id, path);
    private static GraphicsDisplayItem Item(DisplayCollector collector) => Assert.IsType<GraphicsDisplayItem>(Assert.Single(collector.Items));

    [Fact]
    public void Address_copies_member_ids_and_exposes_only_readonly_local_identity()
    {
        var path = new List<string> { "rotated-bounds", "center" };
        var address = new DeclaredElementAddress("blobs", long.MaxValue, path);
        path[0] = "renamed"; path.Clear();
        Assert.Equal("blobs", address.FieldId); Assert.Equal(long.MaxValue, address.ElementId);
        Assert.Equal(new[] { "rotated-bounds", "center" }, address.MemberFieldPath);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)address.MemberFieldPath)[0] = "mutated");
        Assert.Empty(new DeclaredElementAddress("port", 0, Array.Empty<string>()).MemberFieldPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Address_rejects_missing_output_field_identity(string? fieldId) =>
        Assert.ThrowsAny<ArgumentException>(() => new DeclaredElementAddress(fieldId!, 0, Array.Empty<string>()));

    [Fact]
    public void Address_rejects_negative_id_null_path_and_invalid_member_identity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeclaredElementAddress("blobs", -1, Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => new DeclaredElementAddress("blobs", 0, null!));
        foreach (var invalid in new[] { null, "", " " })
            Assert.Throws<ArgumentException>(() => new DeclaredElementAddress("blobs", 0, new[] { "member", invalid! }));
    }

    [Fact]
    public void Batch_copies_outer_contour_and_address_lists_without_claiming_point_payload_immutability()
    {
        var first = Contour(7); var second = Contour(2);
        var contours = new List<Point[]> { first, second };
        var firstAddress = Address(7, "contour"); var secondAddress = Address(2, "rotated-bounds");
        var addresses = new List<DeclaredElementAddress> { firstAddress, secondAddress };
        var batch = new BatchedContourGeometry(contours, false, addresses);
        contours.Reverse(); contours.Clear(); addresses.Reverse(); addresses.Clear();
        Assert.Equal(2, batch.Contours.Count); Assert.False(batch.IsClosed);
        Assert.Same(first, batch.Contours[0]); Assert.Same(second, batch.Contours[1]);
        var sources = Assert.IsAssignableFrom<IReadOnlyList<DeclaredElementAddress>>(batch.SourceElements);
        Assert.Equal(new long[] { 7, 2 }, sources.Select(address => address.ElementId));
        Assert.Same(firstAddress, sources[0]); Assert.Same(secondAddress, sources[1]);
        Assert.Throws<NotSupportedException>(() => ((IList<Point[]>)batch.Contours).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<DeclaredElementAddress>)sources)[0] = Address(99));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void Explicit_mapping_count_must_match_before_any_collector_mutation(int contourCount, int addressCount)
    {
        var collector = new DisplayCollector();
        var contours = Enumerable.Range(0, contourCount).Select(index => Contour(index)).ToArray();
        var addresses = Enumerable.Range(0, addressCount).Select(index => Address(index, "contour")).ToArray();
        Assert.Throws<ArgumentException>(() => new BatchedContourGeometry(contours, sourceElements: addresses));
        Assert.Throws<ArgumentException>(() => collector.AddContourBatch(new GraphicsStyle(), contours, sourceElements: addresses));
        Assert.False(collector.HasItems); Assert.Empty(collector.Items);
    }

    [Fact]
    public void Invalid_address_or_null_geometry_cannot_partially_add_a_style_or_batch()
    {
        var collector = new DisplayCollector(); var style = new GraphicsStyle();
        Assert.Throws<ArgumentException>(() => collector.AddContourBatch(style, new[] { Contour(0) }, sourceElements: new DeclaredElementAddress[] { null! }));
        Assert.Throws<ArgumentNullException>(() => collector.AddContourBatch(style, null!, sourceElements: new[] { Address(1) }));
        Assert.Throws<ArgumentNullException>(() => new BatchedContourGeometry(null!));
        Assert.Empty(collector.Items);
        collector.AddContourBatch(style, new[] { Contour(5) }, sourceElements: new[] { Address(5) });
        var item = Item(collector); var original = Assert.Single(item.Geometries);
        Assert.Throws<ArgumentException>(() => collector.AddContourBatch(style, new[] { Contour(1) }, false, sourceElements: Array.Empty<DeclaredElementAddress>()));
        Assert.Same(original, Assert.Single(Item(collector).Geometries)); Assert.Equal(new[] { true }, item.IsPassArray);
    }

    [Fact]
    public void Producer_sort_order_retains_each_objects_explicit_id_and_member_through_collector_freeze()
    {
        // The contour and address originate from the SAME object before/after sorting.
        var blobs = new[] { new SourceBlob(40, 3, Contour(40)), new SourceBlob(7, 9, Contour(7)), new SourceBlob(90, 6, Contour(90)) };
        var collector = new DisplayCollector(); var style = new GraphicsStyle();
        foreach (var sorted in new[] { blobs.OrderBy(blob => blob.Area).ToArray(), blobs.OrderByDescending(blob => blob.Area).ToArray() })
            collector.AddContourBatch(style, sorted.Select(blob => blob.Contour).ToArray(),
                sourceElements: sorted.Select(blob => Address(blob.Id, "contour")).ToArray());
        var item = Item(collector); Assert.Equal(GraphicsType.Contour, item.Type); Assert.Same(style, item.Style);
        var batches = item.Geometries.Cast<BatchedContourGeometry>().ToArray();
        Assert.Equal(new long[] { 40, 90, 7 }, batches[0].SourceElements!.Select(address => address.ElementId));
        Assert.Equal(new long[] { 7, 90, 40 }, batches[1].SourceElements!.Select(address => address.ElementId));
        foreach (var batch in batches)
            for (var index = 0; index < batch.Contours.Count; index++)
            {
                Assert.Equal(batch.Contours[index][0].X, (double)batch.SourceElements![index].ElementId);
                Assert.Equal("blobs", batch.SourceElements[index].FieldId);
                Assert.Equal(new[] { "contour" }, batch.SourceElements[index].MemberFieldPath);
            }
    }

    [Fact]
    public void Same_style_ok_ng_and_unmapped_batches_keep_separate_metadata_with_equal_lengths()
    {
        var collector = new DisplayCollector(); var style = new GraphicsStyle { Name = "same display name" };
        collector.AddContourBatch(style, new[] { Contour(12), Contour(4) }, true, false,
            new[] { Address(12, "contour"), Address(4, "contour") });
        collector.AddContourBatch(style, new[] { Contour(99), Contour(3) }, false,
            sourceElements: new[] { Address(99, "rotated-bounds"), Address(3, "rotated-bounds") });
        collector.AddContourBatch(style, new[] { Contour(12), Contour(4) });
        var item = Item(collector); Assert.Equal(new[] { true, false, true }, item.IsPassArray);
        var batches = item.Geometries.Cast<BatchedContourGeometry>().ToArray();
        Assert.False(batches[0].IsClosed); Assert.True(batches[1].IsClosed);
        Assert.Equal(new long[] { 12, 4 }, batches[0].SourceElements!.Select(address => address.ElementId));
        Assert.Equal(new long[] { 99, 3 }, batches[1].SourceElements!.Select(address => address.ElementId));
        Assert.All(batches[1].SourceElements!, address => Assert.Equal(new[] { "rotated-bounds" }, address.MemberFieldPath));
        Assert.Null(batches[2].SourceElements); // Same count/style/name does not infer a map.
    }

    [Fact]
    public void Reading_items_then_appending_a_batch_invalidates_only_the_finalized_cache()
    {
        var collector = new DisplayCollector(); var style = new GraphicsStyle();
        collector.AddContourBatch(style, new[] { Contour(6) }, sourceElements: new[] { Address(6, "contour") });
        var first = Assert.IsType<BatchedContourGeometry>(Assert.Single(Item(collector).Geometries));
        collector.AddContourBatch(style, new[] { Contour(2) }, false, sourceElements: new[] { Address(2, "rotated-bounds") });
        var item = Item(collector); Assert.Equal(2, item.Geometries.Length); Assert.Same(first, item.Geometries[0]);
        Assert.Equal(6, first.SourceElements![0].ElementId);
        var appended = Assert.IsType<BatchedContourGeometry>(item.Geometries[1]);
        Assert.Equal(2, appended.SourceElements![0].ElementId); Assert.Equal(new[] { true, false }, item.IsPassArray);
    }

    [Fact]
    public void Reading_items_then_merging_same_style_keeps_original_and_merged_batch_addresses()
    {
        var collector = new DisplayCollector(); var incoming = new DisplayCollector(); var style = new GraphicsStyle();
        collector.AddContourBatch(style, new[] { Contour(8) }, sourceElements: new[] { Address(8, "contour") });
        var original = Assert.Single(Item(collector).Geometries);
        incoming.AddContourBatch(style, new[] { Contour(21) }, false, sourceElements: new[] { Address(21, "rotated-bounds") });
        var merged = Assert.Single(Item(incoming).Geometries);
        collector.Merge(incoming);
        var item = Item(collector); Assert.Same(original, item.Geometries[0]); Assert.Same(merged, item.Geometries[1]);
        Assert.Equal(new long[] { 8, 21 }, item.Geometries.Cast<BatchedContourGeometry>().Select(batch => batch.SourceElements![0].ElementId));
        Assert.Equal(new[] { true, false }, item.IsPassArray);
    }

    [Fact]
    public void Merge_of_equal_named_distinct_styles_does_not_cross_assign_sources()
    {
        var collector = new DisplayCollector(); var incoming = new DisplayCollector();
        collector.AddContourBatch(new GraphicsStyle { Name = "same" }, new[] { Contour(2) }, sourceElements: new[] { Address(2, "contour") });
        _ = collector.Items;
        incoming.AddContourBatch(new GraphicsStyle { Name = "same" }, new[] { Contour(9) });
        collector.Merge(incoming);
        var items = collector.Items.Cast<GraphicsDisplayItem>().ToArray(); Assert.Equal(2, items.Length);
        Assert.Equal(2, Assert.IsType<BatchedContourGeometry>(Assert.Single(items[0].Geometries)).SourceElements![0].ElementId);
        Assert.Null(Assert.IsType<BatchedContourGeometry>(Assert.Single(items[1].Geometries)).SourceElements);
    }

    [Fact]
    public void Empty_and_legacy_unmapped_calls_never_fabricate_an_address_and_clear_resets_collection()
    {
        var collector = new DisplayCollector(); var style = new GraphicsStyle();
        collector.AddContourBatch(style, null!); collector.AddContourBatch(style, Array.Empty<Point[]>());
        collector.AddContourBatch(style, Array.Empty<Point[]>(), sourceElements: Array.Empty<DeclaredElementAddress>());
        Assert.Empty(collector.Items);
        var empty = new BatchedContourGeometry(Array.Empty<Point[]>(), sourceElements: Array.Empty<DeclaredElementAddress>());
        Assert.NotNull(empty.SourceElements); Assert.Empty(empty.SourceElements);
        collector.AddContourBatch(style, new[] { Contour(5) });
        var original = Assert.IsType<BatchedContourGeometry>(Assert.Single(Item(collector).Geometries)); Assert.Null(original.SourceElements);
        collector.Clear(); Assert.Empty(collector.Items);
        collector.AddContourBatch(style, new[] { Contour(4) }, sourceElements: new[] { Address(4) });
        var current = Assert.IsType<BatchedContourGeometry>(Assert.Single(Item(collector).Geometries));
        Assert.NotSame(original, current); Assert.Equal(4, current.SourceElements![0].ElementId); Assert.Null(original.SourceElements);
    }

    private sealed record SourceBlob(long Id, double Area, Point[] Contour);
}
