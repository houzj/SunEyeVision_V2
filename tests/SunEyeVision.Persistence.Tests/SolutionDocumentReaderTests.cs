using System.Text;
using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class SolutionDocumentReaderTests
{
    private sealed class Resource : IDisposable { public bool Released; public void Dispose()=>Released=true; }
    private static readonly string Valid="{\"Id\":\"s\",\"Name\":\"s\",\"SchemaVersion\":4,\"Workflows\":[{\"Id\":\"f\",\"Name\":\"f\",\"Nodes\":[{\"Id\":\"n\",\"Name\":\"node\",\"ToolType\":\"tool\",\"Configuration\":{\"ToolId\":\"tool\",\"Fields\":{\"threshold\":{\"Kind\":\"Constant\",\"Value\":7}},\"CurrentValues\":{},\"ParameterBindingOrder\":[]}}],\"Connections\":[]}]}";
    private static IReadOnlyDictionary<string,FrozenToolDeclarations> Declarations()
    {
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("threshold",TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        return new Dictionary<string,FrozenToolDeclarations>{{"tool",new("tool",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,Array.Empty<ResourceFieldDeclaration>(),"admitted"),Array.Empty<ManualReferenceSource>())}};
    }
    private static SolutionReadCandidate Read(string json,DecodedResourcesScope scope)=>SolutionDocumentReader.Read(Encoding.UTF8.GetBytes(json),Declarations(),new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),SolutionSerializationOptions.Configure(new JsonSerializerOptions()),scope);
    [Fact] public void Reads_typed_candidate_and_transfers_ownership_only_once()
    {
        var scope=new DecodedResourcesScope();var resource=new Resource();scope.RegisterNew(resource);
        using var result=Read(Valid,scope);var node=Assert.Single(Assert.Single(result.Workflows).Nodes);
        Assert.Equal(7L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("threshold")).Value);
        using var owner=result.TransferResources();result.Dispose();Assert.False(resource.Released);
        owner.Dispose();Assert.True(resource.Released);Assert.Throws<InvalidOperationException>(()=>result.TransferResources());
    }
    [Theory]
    [InlineData("\"SchemaVersion\":3")]
    [InlineData("\"SchemaVersion\":4,\"schemaversion\":4")]
    [InlineData("\"SchemaVersion\":4,\"GlobalVariables\":[{\"Id\":\"v\",\"ConsumerBindings\":[]}]")]
    public void Rejected_version_or_obsolete_field_releases_candidate_resources(string version)
    {
        var scope=new DecodedResourcesScope();var resource=new Resource();scope.RegisterNew(resource);
        Assert.Throws<JsonException>(()=>Read("{\"Id\":\"s\",\"Name\":\"s\","+version+"}",scope));Assert.True(resource.Released);
    }
    [Fact] public void Unknown_declared_field_or_old_node_configuration_is_rejected()
    {
        var bad=Valid.Replace("\"threshold\":{\"Kind\"", "\"unknown\":{\"Kind\"");
        using var scope=new DecodedResourcesScope();Assert.Throws<JsonException>(()=>Read(bad,scope));
        using var next=new DecodedResourcesScope();Assert.Throws<JsonException>(()=>Read(Valid.Replace("\"Configuration\"","\"RuntimeConfiguration\""),next));
    }
    [Fact] public void Additional_data_survives_the_source_json_lifetime_at_all_candidate_levels()
    {
        var json=Valid.Replace("\"Name\":\"s\",\"SchemaVersion\"", "\"Name\":\"s\",\"AdditionalData\":{\"root\":{\"x\":1}},\"SchemaVersion\"")
            .Replace("\"Name\":\"f\",\"Nodes\"", "\"Name\":\"f\",\"AdditionalData\":{\"flow\":[1,2]},\"Nodes\"")
            .Replace("\"Name\":\"node\",\"ToolType\"", "\"Name\":\"node\",\"AdditionalData\":{\"node\":true},\"ToolType\"");
        using var scope=new DecodedResourcesScope(); using var result=Read(json,scope);
        Assert.Equal(1,result.AdditionalData!["root"].GetProperty("x").GetInt32());
        var flow=Assert.Single(result.Workflows); Assert.Equal(2,flow.AdditionalData!["flow"].GetArrayLength());
        Assert.True(Assert.Single(flow.Nodes).AdditionalData!["node"].GetBoolean());
    }
    [Fact] public void Device_additional_data_is_read_as_nested_data()
    {
        var device=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new SunEyeVision.Domain.Solutions.Device { Id="d" }))!;
        device["AdditionalData"]=System.Text.Json.Nodes.JsonNode.Parse("{\"detail\":{\"n\":4}}");
        var json=Valid.Replace("\"SchemaVersion\":4", "\"Devices\":["+device.ToJsonString()+"],\"SchemaVersion\":4");
        using var scope=new DecodedResourcesScope(); using var result=Read(json,scope);
        Assert.Equal(4,Assert.Single(result.Devices).AdditionalData!["detail"].GetProperty("n").GetInt32());
    }
    [Fact] public void Declared_resource_plan_uses_captured_configuration_and_base_directory()
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("path",TypeRef.ScalarOf(ScalarKind.Text),EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        var declared=new FrozenToolDeclarations("tool",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,new[]{new ResourceFieldDeclaration("path",ResourcePurpose.FileRead,ResourceReferenceCardinality.Scalar)},"admitted"),Array.Empty<ManualReferenceSource>());
        var json=Valid.Replace("\"threshold\":{\"Kind\":\"Constant\",\"Value\":7}","\"path\":{\"Kind\":\"Constant\",\"Value\":\"image.png\"}");
        using var scope=new DecodedResourcesScope();
        using var result=SolutionDocumentReader.Read(Encoding.UTF8.GetBytes(json),new Dictionary<string,FrozenToolDeclarations>{{"tool",declared}},new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new JsonSerializerOptions(),scope,baseDirectory:root);
        var resource=Assert.Single(result.ResourcePlan);Assert.Equal(Path.Combine(root,"image.png"),resource.AbsolutePath);Assert.False(resource.Deferred);
        Assert.Contains(result.Diagnostics,d=>d.Code=="RESOURCE_UNAVAILABLE");
    }
}



