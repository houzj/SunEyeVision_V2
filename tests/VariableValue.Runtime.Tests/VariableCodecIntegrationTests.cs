using System.Text.Json;
using OpenCvSharp;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class VariableCodecIntegrationTests
{
    private static JsonSerializerOptions Options(DecodedResourcesScope scope) { var options=new JsonSerializerOptions();options.Converters.Add(new GlobalVariableValueConverter(new SdkVariableValueExtensionCodec(scope)));return options; }
    [Fact] public void Current_v3_wrapper_bytes_match_original_converter_for_actual_values()
    {
        using var scope=new DecodedResourcesScope();
        var expected=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"variable-values-v3.golden.txt"));
        using var image=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23));
        object[] values={7L,2.5,"汉字<&",true,new long[]{1,2},new double[]{1.25,2.5},new BinaryPayload(new byte[]{1,2,3}),new AffineMatrix(1,2,3,4,5,6),new RoiShape(),image};
        Assert.Equal(expected,values.Select(value => JsonSerializer.Serialize(value,typeof(object),Options(scope))).ToArray());
    }
    [Theory]
    [InlineData("Integer")] [InlineData("integer")] [InlineData("Integer[]")] [InlineData("integer[]")]
    [InlineData("Double")] [InlineData("Double[]")] [InlineData("Float")] [InlineData("Float[]")]
    [InlineData("String")] [InlineData("String[]")] [InlineData("Boolean")] [InlineData("Boolean[]")]
    [InlineData("boolean[]")] [InlineData("Image")] [InlineData("ROI")] [InlineData("Unknown")]
    public void Neutral_pure_declaration_matches_existing_binding_contract(string vocabulary)
    {
        var original = vocabulary switch { "Integer[]"=>typeof(long[]),"Double[]"=>typeof(double[]),"String[]"=>typeof(string[]),"Boolean[]"=>typeof(bool[]),_=>SdkTypeVocabulary.ToSystemType(vocabulary) };
        var prior = original is not null && BindingClrAdapter.TryDescribe(original,out var type,out var storage)
            && (type.Shape==ValueShape.Scalar || type.Shape==ValueShape.Sequence && type.Element!.Shape==ValueShape.Scalar);
        var current=VariableValueDeclarations.TryGetPort(vocabulary,out var port);
        Assert.Equal(prior,current);
        if(!prior)return;
        Assert.True(BindingClrAdapter.TryDescribe(original!,out var expectedType,out var expectedStorage));
        Assert.Equal(expectedType,port.Type);Assert.Equal(expectedStorage,port.Storage);
    }
    [Fact] public void Binary_and_matrix_use_real_sdk_types()
    {
        using var scope=new DecodedResourcesScope();
        var binary=new BinaryPayload(new byte[]{1,2,3});
        var json=JsonSerializer.Serialize((object)binary,Options(scope));
        Assert.Equal(binary.ToBase64(),Assert.IsType<BinaryPayload>(JsonSerializer.Deserialize<object>(json,Options(scope))).ToBase64());
        var matrix=new AffineMatrix(1,2,3,4,5,6);
        json=JsonSerializer.Serialize((object)matrix,Options(scope));
        var actual=Assert.IsType<AffineMatrix>(JsonSerializer.Deserialize<object>(json,Options(scope)));
        Assert.Equal(matrix.M11,actual.M11);Assert.Equal(matrix.OffsetY,actual.OffsetY);
    }
    [Fact] public void Real_variable_property_uses_extension_without_hijacking_other_object_properties()
    {
        using var scope=new DecodedResourcesScope();
        var options=SolutionSerializationOptions.Configure(new JsonSerializerOptions(),new SdkVariableValueExtensionCodec(scope));
        var variable=new GlobalVariable("图片",new Mat(2,3,MatType.CV_8UC1,Scalar.All(23)),"Image");
        using var original=Assert.IsType<Mat>(variable.Value);
        var json=JsonSerializer.Serialize(variable,options);
        var restored=JsonSerializer.Deserialize<GlobalVariable>(json,options)!;
        var loaded=Assert.IsType<Mat>(restored.Value);
        Assert.Equal((byte)23,loaded.At<byte>(0,0));
        var communication=new Communication { Name="设备",Settings=new(){["port"]=123L} };
        json=JsonSerializer.Serialize(communication,options);
        Assert.Contains("\"port\":123",json);
    }
    [Fact] public void Invalid_resources_and_unsupported_carriers_fail_explicitly()
    {
        using var scope=new DecodedResourcesScope();
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"!\"}",Options(scope)));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Transform\",\"Value\":{}}",Options(scope)));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"AA==\"}",Options(scope)));
        using var empty=new Mat();
        Assert.Throws<JsonException>(()=>JsonSerializer.Serialize((object)empty,Options(scope)));
    }
    [Fact] public void Roi_shape_preserves_real_sdk_type()
    {
        using var scope=new DecodedResourcesScope();
        var shape=new RoiShape { Geometry=new SunEyeVision.Plugin.SDK.Models.Geometry.Point2d(12.5,34.25) };
        var json=JsonSerializer.Serialize((object)shape,Options(scope));
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"roi-v3.golden.txt")),json);
        var loaded=Assert.IsType<RoiShape[]>(JsonSerializer.Deserialize<object>(json,Options(scope)));
        var point=Assert.IsType<SunEyeVision.Plugin.SDK.Models.Geometry.Point2d>(Assert.Single(loaded).Geometry);
        Assert.Equal(12.5,point.X); Assert.Equal(34.25,point.Y);
    }
    [Fact] public void Image_roundtrip_owns_a_new_real_mat()
    {
        using var scope=new DecodedResourcesScope();
        using var image=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23));
        var json=JsonSerializer.Serialize((object)image,Options(scope));
        var loaded=Assert.IsType<Mat>(JsonSerializer.Deserialize<object>(json,Options(scope)));
        Assert.NotSame(image,loaded);Assert.Equal(2,loaded.Rows);Assert.Equal(3,loaded.Cols);Assert.Equal((byte)23,loaded.At<byte>(0,0));
    }
    [Fact] public void Decoded_image_is_disposed_on_failed_candidate_and_transfer_is_single_owner()
    {
        using var input=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23));
        using var writerScope=new DecodedResourcesScope();
        var json=JsonSerializer.Serialize((object)input,Options(writerScope));
        var failedScope=new DecodedResourcesScope();
        var failed=Assert.IsType<Mat>(JsonSerializer.Deserialize<object>(json,Options(failedScope)));
        failedScope.Dispose(); Assert.True(failed.IsDisposed);
        var scope=new DecodedResourcesScope();
        var published=Assert.IsType<Mat>(JsonSerializer.Deserialize<object>(json,Options(scope)));
        using var owner=scope.Transfer(); scope.Dispose(); Assert.False(published.IsDisposed);
        Assert.Throws<InvalidOperationException>(()=>scope.Transfer());
        owner.Dispose(); Assert.True(published.IsDisposed); owner.Dispose();
    }
    private sealed class ObservingCodec(SdkVariableValueExtensionCodec inner) : IVariableValueExtensionCodec
    {
        public DecodedResourcesScope Scope => inner.Scope;
        public Mat? LastImage { get; private set; }
        public bool SupportsType(string name)=>inner.SupportsType(name);
        public object? Read(string name,JsonElement value,JsonSerializerOptions options)
        { var decoded=inner.Read(name,value,options);LastImage=decoded as Mat ?? LastImage;return decoded; }
        public bool TryWrite(object value,JsonSerializerOptions options,out string typeName,out object? serializedValue)=>inner.TryWrite(value,options,out typeName,out serializedValue);
    }
    [Fact] public void Failed_solution_parse_releases_image_created_earlier()
    {
        using var input=new Mat(2,3,MatType.CV_8UC1,Scalar.All(23)); using var writeScope=new DecodedResourcesScope();
        var wrapper=JsonSerializer.Serialize((object)input,Options(writeScope));
        var json="{\"Id\":\"s\",\"Name\":\"s\",\"SchemaVersion\":4,\"GlobalVariables\":[{\"Id\":\"v\",\"Name\":\"image\",\"Type\":\"Image\",\"Value\":"+wrapper+"}],\"CommunicationResponses\":123}";
        var bytes=System.Text.Encoding.UTF8.GetBytes(json);var scope=new DecodedResourcesScope();var codec=new ObservingCodec(new SdkVariableValueExtensionCodec(scope));
        var vocabulary=SdkVariableVocabularyCapture.FromCandidate(bytes);
        Assert.ThrowsAny<Exception>(()=>SolutionDocumentReader.Read(bytes,new Dictionary<string,FrozenToolDeclarations>(),vocabulary,new JsonSerializerOptions(),scope,codec));
        Assert.NotNull(codec.LastImage);Assert.True(codec.LastImage!.IsDisposed);
    }
    [Fact] public void Reader_rejects_a_codec_owned_by_another_scope()
    {
        var bytes=System.Text.Encoding.UTF8.GetBytes("{\"Id\":\"s\",\"Name\":\"s\",\"SchemaVersion\":4}");
        using var first=new DecodedResourcesScope();using var second=new DecodedResourcesScope();
        Assert.Throws<InvalidOperationException>(()=>SolutionDocumentReader.Read(bytes,new Dictionary<string,FrozenToolDeclarations>(),new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new JsonSerializerOptions(),first,new SdkVariableValueExtensionCodec(second)));
    }
}















