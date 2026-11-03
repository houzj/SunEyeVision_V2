using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.Contracts;
using Xunit;

public sealed class SolutionDocumentWriterTests
{
    [Fact] public void Relative_resources_cannot_silently_change_base_directory_on_save_as()
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        var source=Path.Combine(root,"source"); var destination=Path.Combine(root,"destination");
        var bytes=System.Text.Encoding.UTF8.GetBytes("{\"Id\":\"s\",\"SchemaVersion\":4}");
        var encoded=new EncodedSolutionDocument(bytes,Array.Empty<PersistenceDiagnostic>(),new[]{new FrozenResourceReference("$.path","image.png",ResourcePurpose.FileRead,Path.Combine(source,"image.png"))},source);
        Assert.Throws<InvalidOperationException>(()=>encoded.BindTarget(Path.Combine(destination,"solution.json")));
        Assert.Equal(Path.Combine(source,"solution.json"),encoded.BindTarget(Path.Combine(source,"solution.json")).TargetPath);
    }
    [Fact] public void Save_as_rebases_declared_relative_resource_and_revalidates()
    {
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N"));
        var source=Path.Combine(root,"source"); var target=Path.Combine(root,"target"); Directory.CreateDirectory(source); Directory.CreateDirectory(target);
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("path",TypeRef.ScalarOf(ScalarKind.Text),EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        var declaration=new FrozenToolDeclarations("tool",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,new[]{new ResourceFieldDeclaration("path",ResourcePurpose.FileRead,ResourceReferenceCardinality.Scalar)},"admitted"),Array.Empty<ManualReferenceSource>());
        var doc=new SolutionDefinition("s","s"); var flow=new WorkflowDefinition("f","f");
        var node=new NodeDefinition("n","n","tool",new ToolConfiguration("tool",new FieldSettings(settings.Inputs,new Dictionary<string,ParameterSetting>{{"path",new ConstantSetting("image.png")}})));
        ((List<NodeDefinition>)typeof(WorkflowDefinition).GetProperty("MutableNodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(flow)!).Add(node);
        ((List<WorkflowDefinition>)typeof(SolutionDefinition).GetProperty("MutableWorkflows",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(doc)!).Add(flow);
        var declarations=new Dictionary<string,FrozenToolDeclarations>{{"tool",declaration}}; var types=new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>());
        var capture=SolutionDocumentWriter.Capture(doc,declarations,types,new JsonSerializerOptions(),baseDirectory:source);
        Assert.Throws<InvalidOperationException>(()=>capture.BindTarget(Path.Combine(target,"file.solution")));
        var rebased=capture.Rebase(target,declarations,types,new JsonSerializerOptions());
        var targetFile=Path.Combine(target,"file.solution"); SolutionFileCommit.Write(rebased.BindTarget(targetFile,TargetExpectation.Missing),Path.Combine(target,"recovery"));
        using var scope=new DecodedResourcesScope(); using var restored=SolutionDocumentReader.Read(File.ReadAllBytes(targetFile),declarations,types,new JsonSerializerOptions(),scope,baseDirectory:target);
        Assert.Equal(Path.Combine(source,"image.png"),Assert.Single(restored.ResourcePlan).AbsolutePath);
    }
    [Fact] public void New_document_with_edited_node_is_encoded_then_strictly_reloaded_and_saved()
    {
        var document=new SolutionDefinition("solution","current"); var flow=new WorkflowDefinition("flow","flow");
        var empty=new ToolSchema(Array.Empty<Port>(),Array.Empty<Port>());
        var settings=new ToolSchema(new[]{new Port("threshold",TypeRef.ScalarOf(ScalarKind.Int64),Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant)},Array.Empty<Port>());
        var declaration=new FrozenToolDeclarations("tool",empty,settings,empty,new Dictionary<string,ParameterReferenceBehavior>(),new(null,Array.Empty<ResourceFieldDeclaration>(),"admitted"),Array.Empty<ManualReferenceSource>());
        var config=new ToolConfiguration("tool",new FieldSettings(settings.Inputs,new Dictionary<string,ParameterSetting>{{"threshold",new ConstantSetting(8L)}}));
        var node=new NodeDefinition("node","changed","tool",config);
        ((List<NodeDefinition>)typeof(WorkflowDefinition).GetProperty("MutableNodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(flow)!).Add(node);
        ((List<WorkflowDefinition>)typeof(SolutionDefinition).GetProperty("MutableWorkflows",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(document)!).Add(flow);
        var declarations=new Dictionary<string,FrozenToolDeclarations>{{"tool",declaration}};
        var types=new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>());
        var encoded=SolutionDocumentWriter.Capture(document,declarations,types,new JsonSerializerOptions());
        typeof(NodeDefinition).GetProperty(nameof(NodeDefinition.Name))!.SetValue(node,"edited-again");
        var second=SolutionDocumentWriter.Capture(document,declarations,types,new JsonSerializerOptions());
        Assert.NotEqual(encoded.CopyBytes(),second.CopyBytes());
        using(var secondScope=new DecodedResourcesScope())
        using(var changed=SolutionDocumentReader.Read(second.CopyBytes(),declarations,types,new JsonSerializerOptions(),secondScope))
            Assert.Equal("edited-again",Assert.Single(Assert.Single(changed.Workflows).Nodes).Name);
        var root=Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var target=Path.Combine(root,"edited.solution"); var saved=SolutionFileCommit.Write(encoded.BindTarget(target,TargetExpectation.Missing),Path.Combine(root,"backup.solution"));
        Assert.Equal(encoded.CopyBytes(),saved.WrittenSnapshot.CopyBytes());
        using var scope=new DecodedResourcesScope(); using var candidate=SolutionDocumentReader.Read(File.ReadAllBytes(target),declarations,types,new JsonSerializerOptions(),scope);
        var restored=Assert.Single(Assert.Single(candidate.Workflows).Nodes);
        Assert.Equal("changed",restored.Name); Assert.Equal(8L,Assert.IsType<ConstantSetting>(restored.Configuration.Fields.Read("threshold")).Value);
    }
}

