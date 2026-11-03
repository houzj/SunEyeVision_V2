using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Services.Interaction;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Canvas UI")]
public sealed class CanvasSessionTests
{
    [Fact] public void Connection_move_path_enable_and_history_edit_the_same_actual_nodes()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var source=f.Add();var target=f.Add("BlobAnalysis",350,290);var wire=f.Connect(source,target,"LeftPort","RightPort");
        var setting=Assert.IsType<ReferenceSetting>(target.Configuration.Fields.Read("input.image"));Assert.Equal(source.Id,setting.Reference.NodeId);Assert.Equal(new[]{"data"},setting.Reference.MemberFieldPath);
        var path=new[]{new CanvasPoint(150,120),new CanvasPoint(250,120),new CanvasPoint(250,290),new CanvasPoint(350,290)};
        f.Canvas.EditPath(wire,path);var original=source.Position;var projection=f.Projection(source);
        f.Canvas.MoveNodes(new Dictionary<CanvasNodeProjection,CanvasPoint>{{projection,new(200,160)}});
        Assert.Equal(new CanvasPoint(200,160),source.Position);Assert.Null(wire.Definition.ManualPathPoints);
        f.Session.Undo();Assert.Equal(original,source.Position);Assert.Equal(path,wire.Definition.ManualPathPoints);Assert.Same(source,f.Session.FindNode(f.Root.Id,source.Id));
        f.Session.Redo();Assert.Equal(new CanvasPoint(200,160),source.Position);Assert.Null(wire.Definition.ManualPathPoints);
        f.Canvas.SetEnabled(f.Projection(target),false);Assert.False(target.IsEnabled);f.Session.Undo();Assert.True(target.IsEnabled);
        f.Canvas.EditPorts(wire,"RightPort","LeftPort");Assert.Equal("RightPort",wire.SourcePort);f.Session.Undo();Assert.Equal("LeftPort",wire.SourcePort);
    });
    [Fact] public void Insert_delete_undo_preserve_ports_and_retire_old_admission_without_reviving_it()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var a=f.Add();var b=f.Add("BlobAnalysis",450,300);var edge=f.Connect(a,b,"LeftPort","RightPort");var middle=f.Add("ImageLoad",300,160);
        f.Canvas.InsertNode(f.Projection(middle),edge,new(275,180));Assert.Equal(2,f.Canvas.Connections.Count);Assert.All(f.Canvas.Connections,item=>{Assert.Equal("LeftPort",item.SourcePort);Assert.Equal("RightPort",item.TargetPort);});
        f.Session.Undo();Assert.Same(edge.Definition,Assert.Single(f.Canvas.Connections).Definition);f.Session.Redo();Assert.Equal(2,f.Canvas.Connections.Count);
        var cap=f.Session.AdmitPresentation(f.Root.Id,middle.Id);var oldProjection=f.Projection(middle);f.Main.SelectedNode=middle;
        f.Canvas.Delete(new[]{oldProjection},Array.Empty<CanvasConnectionProjection>());Assert.False(cap.IsCurrent);Assert.Null(f.Main.SelectedNode);
        f.Session.Undo();Assert.Same(middle,f.Session.FindNode(f.Root.Id,middle.Id));Assert.False(cap.IsCurrent);Assert.True(f.Session.AdmitPresentation(f.Root.Id,middle.Id).IsCurrent);
        Assert.Throws<InvalidOperationException>(()=>f.Canvas.MoveNodes(new Dictionary<CanvasNodeProjection,CanvasPoint>{{oldProjection,new(1,1)}}));
    });
    [Fact] public void Selection_and_inline_rename_use_actual_identity_and_reject_stale_configuration()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();var projection=f.Projection(node);
        f.Main.SelectedNode=node;Assert.Same(node,f.Main.SelectedNode);Assert.Equal(SunEyeVision.Plugin.Wpf.Presenters.Results.ResultViewState.Declaration,f.Main.CurrentResultPublication!.State);Assert.Empty(f.Main.CurrentResultPublication.Schema.Inputs);
        Assert.Throws<InvalidOperationException>(()=>f.Main.SelectedNode=new NodeDefinition(node.Id,node.Name,node.ToolType,node.Configuration));
        Assert.True((bool)UiRuntime.Call(f.View,"BeginCanvasNodeRename",projection)!);projection.RenameBuffer="新名称";Assert.True((bool)UiRuntime.Call(f.View,"CommitCanvasNodeRename",projection,true)!);Assert.Equal("新名称",node.DispName);
        f.Session.Undo();Assert.NotEqual("新名称",node.DispName);f.Session.Redo();Assert.Equal("新名称",node.DispName);
        Assert.True((bool)UiRuntime.Call(f.View,"BeginCanvasNodeRename",projection)!);projection.RenameBuffer="过期名称";f.Fields(node,("sequence.initial",new ConstantSetting(7L)));
        Assert.False((bool)UiRuntime.Call(f.View,"CommitCanvasNodeRename",projection,true)!);Assert.Equal("新名称",node.DispName);Assert.False(projection.IsEditingName);
    });
    [Fact] public void Real_drag_preview_cancel_and_routed_drop_leave_one_atomic_history_entry()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var node=f.Add();var projection=f.Projection(node);projection.IsSelected=true;var opening=node.Position;
        UiRuntime.Call(f.View,"ArmCanvasNodeMove",projection,new Point(opening.X,opening.Y));var move=UiRuntime.Field<object>(f.View,"_canvasNodeMove")!;
        UiRuntime.Call(f.View,"PreviewCanvasNodeMove",move,new Point(opening.X+45,opening.Y+40));Assert.Equal(opening,node.Position);
        Assert.NotEmpty((System.Collections.IEnumerable)move.GetType().GetProperty("Ghosts")!.GetValue(move)!);
        UiRuntime.Call(f.View,"CancelCanvasNodeMove");Assert.Equal(opening,node.Position);
        var count=f.Session.HistoryEntries.Count;UiRuntime.Call(f.View,"ArmCanvasNodeMove",projection,new Point(opening.X,opening.Y));move=UiRuntime.Field<object>(f.View,"_canvasNodeMove")!;
        var token=(string)move.GetType().GetProperty("Token")!.GetValue(move)!;var format=(string)typeof(WorkflowCanvasControl).GetField("CanvasNodeDragFormat",BindingFlags.NonPublic|BindingFlags.Static)!.GetRawConstantValue()!;
        var data=new DataObject();data.SetData(format,token);var canvas=(Canvas)f.View.FindName("WorkflowCanvas");
        var ctor=typeof(DragEventArgs).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Single(item=>item.GetParameters().Length==5);
        var args=(DragEventArgs)ctor.Invoke(new object[]{data,DragDropKeyStates.None,DragDropEffects.Move,canvas,new Point(opening.X+45,opening.Y+40)});args.RoutedEvent=DragDrop.PreviewDropEvent;canvas.RaiseEvent(args);
        Assert.Equal(DragDropEffects.Move,args.Effects);Assert.NotEqual(opening,node.Position);Assert.Equal(count+1,f.Session.HistoryEntries.Count);
        f.Session.Undo();Assert.Equal(opening,node.Position);UiRuntime.Call(f.View,"CancelCanvasGesture");
    });
    [Fact] public void Alignment_enable_and_copy_use_actual_session_transactions()=>UiRuntime.Run(()=>
    {
        using var f=new DocumentUiFixture();var first=f.Add();var second=f.Add("ImageLoad",350,260);f.Projection(first).IsSelected=true;f.Projection(second).IsSelected=true;
        var before=new[]{first.Position,second.Position};var revision=f.Session.HistoryEntries.Count;UiRuntime.Call(f.View,"AlignCanvasSelection",NodeAlignment.Left);Assert.Equal(first.PositionX,second.PositionX);Assert.Equal(revision+1,f.Session.HistoryEntries.Count);
        f.Session.Undo();Assert.Equal(before,new[]{first.Position,second.Position});UiRuntime.Call(f.View,"SetCanvasSelectionEnabled",false);Assert.False(first.IsEnabled);Assert.False(second.IsEnabled);f.Session.Undo();Assert.True(first.IsEnabled);Assert.True(second.IsEnabled);
        var menu=(ContextMenu)UiRuntime.Call(f.View,"CreateCanvasSelectionMenu")!;Assert.True(menu.Items.OfType<MenuItem>().Single(item=>item.Header?.ToString()=="复制").IsEnabled);
        Assert.True(f.Main.CopyCanvasSelection());Assert.True(f.Main.PasteCanvasSelection());UiRuntime.Pump();Assert.Equal(4,f.Canvas.Nodes.Count);
        Assert.Equal(2,f.Canvas.Nodes.Count(item=>item.IsSelected));Assert.True(f.Session.Undo());Assert.Equal(2,f.Canvas.Nodes.Count);
        var entry=Assert.Single(f.Main.FindWorkflowNodesById(first.Id));Assert.Same(first,entry.Node);Assert.True(f.Main.NavigateSearchResult(entry));Assert.Same(first,f.Main.SelectedNode);
    });
}
