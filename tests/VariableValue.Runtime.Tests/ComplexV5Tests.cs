using System.Text.Json;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Domain.Solutions;
using Xunit;

public sealed class ComplexV5Tests
{
    private sealed class UntrustedRaster : IReadOnlyStoredRaster
    {
        public int Calls;
        public StoredRasterLayout Layout { get { Calls++; throw new InvalidOperationException("外部布局不应被调用"); } }
        public IStoredRasterReader AcquireReader() { Calls++; throw new InvalidOperationException("外部读取器不应被调用"); }
    }

    [Fact]
    public void Untrusted_raster_is_rejected_under_value_gate_without_invoking_external_code()
    {
        var image=new UntrustedRaster();
        using var scope=new DecodedResourcesScope();
        var codec=new ComplexVariableValueCodec(scope);
        Assert.False(codec.TryGetToken(image,out _));
        using(var stream=new MemoryStream())using(var writer=new Utf8JsonWriter(stream))
            Assert.Throws<JsonException>(()=>codec.WriteValue(writer,"Image",image,new JsonSerializerOptions()));
        var variables=new[]{new GlobalVariable("伪造图像",image,"Image")};
        lock(GlobalVariable.ValueGate)
            Assert.Throws<NotSupportedException>(()=>VariableArchive.Encode(variables,new JsonSerializerOptions(),codec));
        Assert.Equal(0,image.Calls);
    }

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
        for(var i=0;i<values.Length;i++)
        {
            var before=values[i].CopyFields();var after=Assert.IsType<RecordValue>(restored[i]).CopyFields();
            Assert.Equal(before.Keys.Order(),after.Keys.Order());
            foreach(var key in before.Keys.Where(key=>key is not ("center" or "vertices")))Assert.Equal(before[key],after[key]);
            if(before.TryGetValue("center",out var center))
            {
                var expected=Assert.IsType<RecordValue>(center);var actual=Assert.IsType<RecordValue>(after["center"]);
                Assert.Equal(expected.Get<double>("x"),actual.Get<double>("x"));Assert.Equal(expected.Get<double>("y"),actual.Get<double>("y"));
            }
            if(before.TryGetValue("vertices",out var points))
            {
                var expected=Assert.IsType<SequenceValue>(points);var actual=Assert.IsType<SequenceValue>(after["vertices"]);
                Assert.Equal(expected.Count,actual.Count);
                for(var j=0;j<expected.Count;j++){var a=Assert.IsType<RecordValue>(expected[j]);var b=Assert.IsType<RecordValue>(actual[j]);Assert.Equal(a.Get<double>("x"),b.Get<double>("x"));Assert.Equal(a.Get<double>("y"),b.Get<double>("y"));}
            }
        }
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
        foreach(var item in restored){var actual=Assert.IsType<RecordValue>(item);foreach(var pair in fields)Assert.Equal(pair.Value,actual.Get<double>(pair.Key));}
    }

    [Fact]
    public void Archive_v2_atomic_write_preserves_changed_target_and_rejects_old_versions()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"_validation",Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            using var scope=new DecodedResourcesScope();
            var bytes=VariableArchive.Encode(new[]{new GlobalVariable("消息",new BinaryValue(new byte[]{4,5,6}),"Binary")},new JsonSerializerOptions(),new ComplexVariableValueCodec(scope));
            using var readScope=new DecodedResourcesScope();
            using var candidate=VariableArchive.Read(bytes,VariableVocabularyCapture.FromVariableArchive(bytes),new JsonSerializerOptions(),readScope,new ComplexVariableValueCodec(readScope));
            var path=Path.Combine(root,"variables.json");var recovery=Path.Combine(root,"recovery");
            candidate.WriteValidated(path,recovery,TargetExpectation.Missing);
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
            File.WriteAllText(path,"external change");
            Assert.ThrowsAny<IOException>(()=>candidate.WriteValidated(path,recovery,TargetExpectation.Match(hash)));
            Assert.Equal("external change",File.ReadAllText(path));
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(()=>candidate.WriteValidated(Path.Combine(root,"cancelled.json"),recovery,TargetExpectation.Missing,cancelled.Token));
            Assert.False(File.Exists(Path.Combine(root,"cancelled.json")));
            using var oldScope=new DecodedResourcesScope();
            var old=System.Text.Encoding.UTF8.GetBytes("{\"Format\":\"SunEyeVision.GlobalVariables\",\"FormatVersion\":1,\"Variables\":[]}");
            Assert.Throws<JsonException>(()=>VariableArchive.Read(old,VariableVocabularyCapture.FromVariableArchive(old),new JsonSerializerOptions(),oldScope,new ComplexVariableValueCodec(oldScope)));
            using var oldSolution=JsonDocument.Parse("{\"SchemaVersion\":4}");
            Assert.Throws<JsonException>(()=>SolutionFormatPolicy.ValidateReadable(oldSolution.RootElement));
        }
        finally { Directory.Delete(root,true); }
    }
}
