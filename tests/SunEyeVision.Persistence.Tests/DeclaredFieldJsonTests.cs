using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class DeclaredFieldJsonTests
{
    private static FrozenToolDeclarations Declarations()
    {
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("threshold",TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        return new("tool",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,Array.Empty<ResourceFieldDeclaration>(),"admitted"),Array.Empty<ManualReferenceSource>());
    }
    [Fact] public void Declared_constant_decodes_with_actual_numeric_storage()
    {
        using var document=JsonDocument.Parse("{\"threshold\":{\"Kind\":\"Constant\",\"Value\":7}}");
        var decoded=DeclaredFieldJson.Read(Declarations(),document.RootElement);
        Assert.Equal(7L,Assert.IsType<ConstantSetting>(decoded["threshold"]).Value);
    }
    [Theory]
    [InlineData("{\"unknown\":{\"Kind\":\"Constant\",\"Value\":1}}")]
    [InlineData("{\"threshold\":{\"Kind\":\"Constant\",\"Value\":1,\"Value\":2}}")]
    [InlineData("{\"threshold\":{\"Kind\":\"Constant\",\"Value\":2147483648}}")]
    public void Unknown_duplicate_and_out_of_range_fields_are_rejected(string json)
    {
        using var document=JsonDocument.Parse(json);
        var error=Record.Exception(()=>DeclaredFieldJson.Read(Declarations(),document.RootElement));
        Assert.True(error is JsonException or ArgumentException or OverflowException,$"Unexpected error: {error}");
    }
}


