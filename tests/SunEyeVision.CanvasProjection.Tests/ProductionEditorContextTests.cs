using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.PresentationProbe;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Services.Editing;
using Xunit;
namespace SunEyeVision.CanvasProjection.Tests;
public sealed class ProductionEditorContextTests
{
    private static DocumentSession Session()
    {
        var registration=new ToolComponentRegistration(new ToolComponentMetadata("canvas-probe","PresentationProbe",ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor),typeof(PresentationProbeDefinition));
        var directory=(RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{new[]{registration}})!;
        return DocumentSession.CreateNew(directory);
    }
    [Fact]
    public void Receipt_rebases_the_actual_draft_without_discarding_raw_text_typed_during_publication()
    {
        using var session=Session();var flow=session.GetWorkflows().Single();using var canvas=new DocumentCanvasProjection(session,flow.Id);
        var node=canvas.CreateNode("PresentationProbe",new(20,20));var binds=0;var pending=false;
        using var context=new ProductionEditorContext(session,flow.Id,node,()=>true,_=>binds++);
        context.HasPendingViewEdits=()=>pending;
        context.PreparedDraft.ApplyFields(new Dictionary<string,ParameterSetting>{{"setting",new ConstantSetting(5L)}});
        session.Changed+=(_,_)=>pending=true;
        Assert.True(context.TryCommitEditor(out var error),error);
        Assert.Equal(0,binds);Assert.Equal(5L,node.Configuration.Fields.Get<long>("setting"));
        context.PreparedDraft.ApplyFields(new Dictionary<string,ParameterSetting>{{"setting",new ConstantSetting(9L)}});
        pending=false;
        Assert.True(context.TryCommitEditor(out error),error);
        Assert.Equal(9L,node.Configuration.Fields.Get<long>("setting"));
        Assert.True(session.Undo());Assert.Equal(5L,node.Configuration.Fields.Get<long>("setting"));
    }
    [Fact]
    public void Deleted_actual_node_cannot_publish_or_be_replaced_by_a_same_id_object()
    {
        using var session=Session();var flow=session.GetWorkflows().Single();using var canvas=new DocumentCanvasProjection(session,flow.Id);
        var node=canvas.CreateNode("PresentationProbe",new(20,20));
        using var context=new ProductionEditorContext(session,flow.Id,node,()=>true,_=>{});
        var fake=new NodeDefinition(node.Id,node.Name,node.ToolType,node.Configuration);
        Assert.Throws<InvalidOperationException>(()=>new ProductionEditorContext(session,flow.Id,fake,()=>true,_=>{}));
        canvas.Delete(new[]{canvas.Nodes.Single()},Array.Empty<CanvasConnectionProjection>());
        Assert.False(context.IsValid);Assert.False(context.TryCommitEditor(out _));
        context.Discard();Assert.Equal(0,session.Editing.RegisteredScopeCount);
    }
}

