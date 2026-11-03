using System.Text.Json;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class VariableCodecIntegrationTests
{
    private static JsonSerializerOptions Options(DecodedResourcesScope scope) { var options=new JsonSerializerOptions();options.Converters.Add(new GlobalVariableValueConverter(new ComplexVariableValueCodec(scope)));return options; }
    [Theory]
    [InlineData("Integer")] [InlineData("Integer[]")] [InlineData("Double")] [InlineData("Double[]")]
    [InlineData("String")] [InlineData("String[]")] [InlineData("Boolean")] [InlineData("Boolean[]")]
    public void Text_binding_declarations_remain_available(string token) => Assert.True(VariableValueDeclarations.TryGetPort(token,out _));
    [Fact] public void Pure_scalar_and_sequence_values_keep_their_actual_contents()
    {
        using var scope=new DecodedResourcesScope();
        object[] values={7L,2.5d,"汉字<&",true,new long[]{1,2},new double[]{1.25,2.5},new string[]{"甲","乙"},new bool[]{true,false}};
        foreach(var value in values)
        {
            var restored=JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(value,typeof(object),Options(scope)),Options(scope));
            if(value is Array array)Assert.Equal(array.Cast<object>(),Assert.IsAssignableFrom<Array>(restored).Cast<object>());
            else Assert.Equal(value,restored);
        }
    }
    [Fact] public void Real_variable_property_uses_extension_without_hijacking_other_object_properties()
    {
        using var scope=new DecodedResourcesScope();
        var image=new ComplexVariableValueCodec(scope).CreateRaster(new StoredRasterLayout(3,2,3,StoredRasterFormat.Gray8),new byte[]{23,23,23,23,23,23});
        var options=SolutionSerializationOptions.Configure(new JsonSerializerOptions(),new ComplexVariableValueCodec(scope));
        var variable=new GlobalVariable("图片",image,"Image");
        var json=JsonSerializer.Serialize(variable,options);
        var restored=JsonSerializer.Deserialize<GlobalVariable>(json,options)!;
        using var reader=Assert.IsAssignableFrom<IReadOnlyStoredRaster>(restored.Value).AcquireReader();
        Assert.Equal((byte)23,reader.GetRow(0)[0]);
        var communication=new Communication { Name="设备",Settings=new(){["port"]=123L} };
        Assert.Contains("\"port\":123",JsonSerializer.Serialize(communication,options));
    }
    [Fact] public void Invalid_resources_and_unsupported_carriers_fail_explicitly()
    {
        using var scope=new DecodedResourcesScope();
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"!\"}",Options(scope)));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Transform\",\"Value\":{}}",Options(scope)));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<object>("{\"Type\":\"Image\",\"Value\":\"AA==\"}",Options(scope)));
        Assert.Throws<NotSupportedException>(()=>JsonSerializer.Serialize((object)new object(),Options(scope)));
    }
    [Fact] public void Decoded_image_is_disposed_on_failed_candidate_and_transfer_is_single_owner()
    {
        using var sourceScope=new DecodedResourcesScope();
        var image=new ComplexVariableValueCodec(sourceScope).CreateRaster(new StoredRasterLayout(3,2,3,StoredRasterFormat.Gray8),new byte[]{23,23,23,23,23,23});
        var json=JsonSerializer.Serialize((object)image,Options(sourceScope));
        var failedScope=new DecodedResourcesScope();
        var failed=Assert.IsAssignableFrom<IReadOnlyStoredRaster>(JsonSerializer.Deserialize<object>(json,Options(failedScope)));
        failedScope.Dispose();Assert.Throws<ObjectDisposedException>(()=>failed.AcquireReader());
        var scope=new DecodedResourcesScope();
        var published=Assert.IsAssignableFrom<IReadOnlyStoredRaster>(JsonSerializer.Deserialize<object>(json,Options(scope)));
        using var owner=scope.Transfer();scope.Dispose();using var reader=published.AcquireReader();
        Assert.Throws<InvalidOperationException>(()=>scope.Transfer());owner.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>published.AcquireReader());Assert.Equal((byte)23,reader.GetRow(0)[0]);
    }
    [Fact] public void Reader_rejects_a_codec_owned_by_another_scope()
    {
        var bytes=System.Text.Encoding.UTF8.GetBytes("{\"Id\":\"s\",\"Name\":\"s\",\"SchemaVersion\":5}");
        using var first=new DecodedResourcesScope();using var second=new DecodedResourcesScope();
        Assert.Throws<InvalidOperationException>(()=>SolutionDocumentReader.Read(bytes,new Dictionary<string,FrozenToolDeclarations>(),new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new JsonSerializerOptions(),first,new ComplexVariableValueCodec(second)));
    }
    private sealed class ObservingCodec(DecodedResourcesScope scope) : IVariableValueExtensionCodec
    {
        private readonly ComplexVariableValueCodec _inner=new(scope);
        public DecodedResourcesScope Scope=>scope;
        public IReadOnlyStoredRaster? LastImage { get; private set; }
        public bool SupportsType(string name)=>_inner.SupportsType(name);
        public object? Read(string name,JsonElement value,JsonSerializerOptions options)
        { var decoded=_inner.Read(name,value,options); LastImage=decoded as IReadOnlyStoredRaster ?? LastImage; return decoded; }
        public bool TryGetToken(object value,out string name)=>_inner.TryGetToken(value,out name);
        public void WriteValue(Utf8JsonWriter writer,string name,object value,JsonSerializerOptions options)=>_inner.WriteValue(writer,name,value,options);
    }
    [Fact] public void Failed_solution_parse_releases_image_created_earlier()
    {
        using var sourceScope=new DecodedResourcesScope();
        var image=new ComplexVariableValueCodec(sourceScope).CreateRaster(new StoredRasterLayout(3,2,3,StoredRasterFormat.Gray8),new byte[]{23,23,23,23,23,23});
        var wrapper=JsonSerializer.Serialize((object)image,Options(sourceScope));
        var json="{\"Id\":\"s\",\"Name\":\"s\",\"SchemaVersion\":5,\"GlobalVariables\":[{\"Id\":\"v\",\"Name\":\"image\",\"Type\":\"Image\",\"Value\":"+wrapper+"}],\"CommunicationResponses\":123}";
        var bytes=System.Text.Encoding.UTF8.GetBytes(json);var scope=new DecodedResourcesScope();var codec=new ObservingCodec(scope);
        Assert.ThrowsAny<Exception>(()=>SolutionDocumentReader.Read(bytes,new Dictionary<string,FrozenToolDeclarations>(),VariableVocabularyCapture.FromCandidate(bytes),new JsonSerializerOptions(),scope,codec));
        Assert.NotNull(codec.LastImage); Assert.Throws<ObjectDisposedException>(()=>codec.LastImage!.AcquireReader());
    }
}
