using SunEyeVision.Domain.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class FrozenToolDeclarationsTests
{
    [Fact] public void Captured_collections_do_not_follow_caller_mutation()
    {
        var schema=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("threshold",TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int32)},Array.Empty<Port>());
        var behavior=new Dictionary<string,ParameterReferenceBehavior>{{"threshold",ParameterReferenceBehavior.StrictExactlyOne}};
        var resources=new List<ResourceFieldDeclaration>();
        var manualSources=new List<ManualReferenceSource>{new("output.image",new[]{"data"})};
        var frozen=new FrozenToolDeclarations("tool",schema,settings,schema,behavior,new(null,resources,"fingerprint"),manualSources);
        behavior.Clear(); manualSources.Clear(); Assert.Single(frozen.ReferenceBehaviors); Assert.Empty(frozen.HostDeclarations.ResourceFields);
        var captured=Assert.Single(frozen.ManualReferenceSources); Assert.Equal("output.image",captured.PortId); Assert.Equal(new[]{"data"},captured.MemberPath);
    }
    [Fact] public void Duplicate_field_and_unknown_behavior_are_rejected()
    {
        var schema=new ToolSchema(new[]{new Port("x",TypeRef.ScalarOf(ScalarKind.Text))},Array.Empty<Port>());
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        Assert.Throws<ArgumentException>(()=>new FrozenToolDeclarations("tool",schema,schema,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,Array.Empty<ResourceFieldDeclaration>(),"fingerprint"),Array.Empty<ManualReferenceSource>()));
        Assert.Throws<ArgumentException>(()=>new FrozenToolDeclarations("tool",empty,schema,empty,new Dictionary<string,ParameterReferenceBehavior>{{"missing",ParameterReferenceBehavior.StrictExactlyOne}},new(null,Array.Empty<ResourceFieldDeclaration>(),"fingerprint"),Array.Empty<ManualReferenceSource>()));
    }
}
