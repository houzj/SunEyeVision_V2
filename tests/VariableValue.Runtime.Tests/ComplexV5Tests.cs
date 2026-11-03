using System.Text.Json;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class ComplexV5Tests
{
    private static JsonSerializerOptions Options(DecodedResourcesScope scope)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GlobalVariableValueConverter(new ComplexVariableValueCodec(scope)));
        return options;
    }

    [Theory]
    [InlineData(StoredRasterFormat.Gray8, 1)]
    [InlineData(StoredRasterFormat.Gray16, 2)]
    [InlineData(StoredRasterFormat.Bgr24, 3)]
    [InlineData(StoredRasterFormat.Bgr48, 6)]
    [InlineData(StoredRasterFormat.Bgra32, 4)]
    [InlineData(StoredRasterFormat.Bgra64, 8)]
    public void Pixels_roundtrip_and_reader_survives_owner_release(StoredRasterFormat format, int bytesPerPixel)
    {
        var layout = new StoredRasterLayout(3, 2, 3 * bytesPerPixel, format);
        var pixels = Enumerable.Range(0, layout.BufferLength).Select(i => (byte)(i * 7)).ToArray();
        using var sourceScope = new DecodedResourcesScope();
        var source = new ComplexVariableValueCodec(sourceScope).CreateRaster(layout, pixels);
        var json = JsonSerializer.Serialize((object)source, Options(sourceScope));
        var resultScope = new DecodedResourcesScope();
        var restored = Assert.IsAssignableFrom<IReadOnlyStoredRaster>(JsonSerializer.Deserialize<object>(json, Options(resultScope)));
        using var reader = restored.AcquireReader();
        var owner = resultScope.Transfer();
        owner.Dispose();
        Assert.Equal(pixels, Enumerable.Range(0, layout.Height).SelectMany(i => reader.GetRow(i).ToArray()).ToArray());
        Assert.Throws<ObjectDisposedException>(() => restored.AcquireReader());
    }

    [Fact]
    public void Binary_snapshot_is_detached_and_roundtrips()
    {
        var bytes = new byte[] { 0, 1, 127, 255 };
        var value = new BinaryValue(bytes);
        bytes[0] = 99;
        using var scope = new DecodedResourcesScope();
        var json = JsonSerializer.Serialize((object)value, Options(scope));
        var restored = Assert.IsType<BinaryValue>(JsonSerializer.Deserialize<object>(json, Options(scope)));
        Assert.Equal(new byte[] { 0, 1, 127, 255 }, restored.ToArray());
    }

    private static RecordValue Point(double x, double y) => new(VariableValueDeclarations.StoredRoiPoint,
        new Dictionary<string, object?> { ["x"] = x, ["y"] = y });

    private static RecordValue Shape(string kind, params (string Key, object? Value)[] values)
    {
        var fields = values.ToDictionary(v => v.Key, v => v.Value);
        fields.Add("kind", kind); fields.Add("fill-color-argb", 0x10203040L);
        fields.Add("stroke-color-argb", 0xff406080L); fields.Add("stroke-thickness", 2.25d); fields.Add("opacity", 0.75d);
        return new RecordValue(VariableValueDeclarations.StoredRoiShape, fields);
    }

    [Fact]
    public void All_roi_shapes_roundtrip_including_major_arc_and_vertical_line()
    {
        var p = Point(11, 22);
        var vertices = new SequenceValue(TypeRef.SequenceOf(VariableValueDeclarations.StoredRoiPoint), new object?[] { p, Point(12, 23), Point(13, 24) });
        var values = new[] {
            Shape("point", ("center", p)), Shape("circle", ("center", p), ("radius", 5d)),
            Shape("rectangle", ("center", p), ("width", 8d), ("height", 9d), ("angle-degrees", 17d)),
            Shape("line", ("center", p), ("direction-x", 0d), ("direction-y", 1d)),
            Shape("annulus", ("center", p), ("inner-radius", 2d), ("outer-radius", 5d), ("start-radians", 0d), ("end-radians", 1d)),
            Shape("arc", ("center", p), ("radius", 5d), ("start-radians", 0d), ("end-radians", 1d), ("major-arc", true)),
            Shape("polygon", ("vertices", vertices), ("closed", true)) };
        var sequence = new SequenceValue(VariableValueDeclarations.StoredRoiSequence, values);
        using var scope = new DecodedResourcesScope();
        var json = JsonSerializer.Serialize((object)sequence, Options(scope));
        var restored = Assert.IsType<SequenceValue>(JsonSerializer.Deserialize<object>(json, Options(scope)));
        Assert.Equal(7, restored.Count);
        Assert.True(Assert.IsType<RecordValue>(restored[5]).Get<bool>("major-arc"));
        Assert.Equal(0d, Assert.IsType<RecordValue>(restored[3]).Get<double>("direction-x"));
        Assert.Equal(0xff406080L, Assert.IsType<RecordValue>(restored[0]).Get<long>("stroke-color-argb"));
    }

    [Fact]
    public void Matrix_array_roundtrips_each_coefficient()
    {
        var fields = new Dictionary<string, object?> { ["m11"]=1d, ["m12"]=2d, ["m21"]=3d, ["m22"]=4d, ["offset-x"]=5d, ["offset-y"]=6d };
        var matrix = new RecordValue(VariableValueDeclarations.StoredTransform, fields);
        var sequence = new SequenceValue(TypeRef.SequenceOf(VariableValueDeclarations.StoredTransform), new object?[] { matrix, matrix });
        using var scope = new DecodedResourcesScope();
        var json = JsonSerializer.Serialize((object)sequence, Options(scope));
        var restored = Assert.IsType<SequenceValue>(JsonSerializer.Deserialize<object>(json, Options(scope)));
        Assert.Equal(2, restored.Count);
        Assert.Equal(6d, Assert.IsType<RecordValue>(restored[1]).Get<double>("offset-y"));
    }
}
