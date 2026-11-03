using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using Xunit;
namespace SunEyeVision.Application.Editing.Tests;
public sealed class FragmentBuilderTests
{
 [Fact] public void Empty_fragment_builds_without_a_published_flow()
 {
  var result=FragmentBuilder.Build("source","unpublished-target",Array.Empty<FragmentNodeSnapshot>(),Array.Empty<FragmentEdgeSnapshot>(),
   new Dictionary<string,FragmentIssuedNode>(),new Dictionary<string,string>(),new CanvasPoint(0,0),false);
  Assert.Empty(result.Nodes);Assert.Empty(result.Edges);Assert.Empty(result.NodeMap);Assert.Empty(result.EdgeMap);
 }
 [Fact] public void Stable_issued_identities_remap_reference_and_origin_without_mutating_source()
 {
  var type=TypeRef.ScalarOf(ScalarKind.Int64);var port=new Port("parameter",type,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
  var sourceReference=new OutputReference("a","output",type,scopeId:"source");
  var fields=new FieldSettings(new[]{port},new Dictionary<string,ParameterSetting>{{"parameter",new ReferenceSetting(sourceReference,OriginId:"connection:edge")}});
  var original=new ToolConfiguration("tool",fields,new Dictionary<string,ConstantSetting>{{"parameter",new ConstantSetting(12L)}},parameterBindingOrder:new[]{"parameter"});
  var empty=new ToolConfiguration("tool",new FieldSettings(Array.Empty<Port>(),new Dictionary<string,ParameterSetting>()));
  var nodes=new[]{new FragmentNodeSnapshot("a","tool","源1",true,new(10,20),140,90,null,null,empty),new FragmentNodeSnapshot("b","tool","目标2",true,new(30,40),140,90,null,null,original)};
  var edge=new FragmentEdgeSnapshot("edge","a","b","right","left",new[]{new CanvasPoint(15,25)});
  var ids=new Dictionary<string,FragmentIssuedNode>{{"a",new("new-a",3,3,"3 源3","源3")},{"b",new("new-b",4,4,"4 目标4","目标4")}};
  var edgeIds=new Dictionary<string,string>{{"edge","new-edge"}};
  FragmentBuildResult Build()=>FragmentBuilder.Build("source","unpublished-target",nodes,new[]{edge},ids,edgeIds,new CanvasPoint(100,200),false);
  var first=Build();var second=Build();Assert.NotSame(first.Nodes[0],second.Nodes[0]);Assert.Equal(first.Nodes.Select(node=>node.Id),second.Nodes.Select(node=>node.Id));
  Assert.Equal("new-edge",Assert.Single(first.Edges).Id);Assert.Equal(new CanvasPoint(115,225),Assert.Single(first.Edges).ManualPathPoints![0]);
  var binding=Assert.IsType<ReferenceSetting>(first.NodeMap["b"].Configuration.Fields.Read("parameter"));
  Assert.Equal("new-a",binding.Reference.NodeId);Assert.Equal("unpublished-target",binding.Reference.ScopeId);Assert.Equal("connection:new-edge",binding.OriginId);
  Assert.Null(first.NodeMap["b"].Configuration.ReadCurrentParameter("parameter"));
  Assert.Equal(12L,original.ReadCurrentParameter("parameter")!.Value);Assert.Equal("a",Assert.IsType<ReferenceSetting>(original.Fields.Read("parameter")).Reference.NodeId);
 }
 [Fact] public void Invalid_issued_mapping_is_rejected_before_any_result_is_returned()
 {
  var configuration=new ToolConfiguration("tool",new FieldSettings(Array.Empty<Port>(),new Dictionary<string,ParameterSetting>()));
  var nodes=new[]{new FragmentNodeSnapshot("a","tool","a",true,new(0,0),140,90,null,null,configuration)};
  Assert.Throws<InvalidOperationException>(()=>FragmentBuilder.Build("s","t",nodes,Array.Empty<FragmentEdgeSnapshot>(),
   new Dictionary<string,FragmentIssuedNode>(),new Dictionary<string,string>(),new CanvasPoint(0,0),false));
  var two=new[]{nodes[0],new FragmentNodeSnapshot("b","tool","b",true,new(1,1),140,90,null,null,configuration)};
  var duplicate=new Dictionary<string,FragmentIssuedNode>{{"a",new("new-a",1,1,"a1","a1")},{"b",new("new-b",1,2,"b2","b2")}};
  Assert.Throws<InvalidOperationException>(()=>FragmentBuilder.Build("s","t",two,Array.Empty<FragmentEdgeSnapshot>(),duplicate,
   new Dictionary<string,string>(),new CanvasPoint(0,0),false));
  Assert.Empty(configuration.Fields.Capture());
 }
 [Fact] public void Unchanged_external_reference_retains_only_its_frozen_current_value()
 {
  var type=TypeRef.ScalarOf(ScalarKind.Int64);var port=new Port("parameter",type,EditModes:FieldEditModes.Reference);
  var reference=new ReferenceSetting(new OutputReference("external","output",type,scopeId:"s"));
  var fields=new FieldSettings(new[]{port},new Dictionary<string,ParameterSetting>{{"parameter",reference}});
  var configuration=new ToolConfiguration("tool",fields,new Dictionary<string,ConstantSetting>{{"parameter",new ConstantSetting(12L)}},parameterBindingOrder:new[]{"parameter"});
  var nodes=new[]{new FragmentNodeSnapshot("a","tool","a",true,new(0,0),140,90,null,null,configuration)};
  var result=FragmentBuilder.Build("s","s",nodes,Array.Empty<FragmentEdgeSnapshot>(),
   new Dictionary<string,FragmentIssuedNode>{{"a",new("new-a",1,1,"a1","a1")}},new Dictionary<string,string>(),new CanvasPoint(0,0),true);
  Assert.Equal(12L,Assert.Single(result.Nodes).Configuration.ReadCurrentParameter("parameter")!.Value);
  Assert.Equal(12L,configuration.ReadCurrentParameter("parameter")!.Value);
 }
}
