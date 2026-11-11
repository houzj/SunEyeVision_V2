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
    [Fact]
    public void Node_open_keeps_the_old_selection_through_close_and_rejects_disabled_nodes() => UiRuntime.Run(() =>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 290);
        fixture.ConfigureImage(source, fixture.Image());
        fixture.Connect(source, blob);
        fixture.Canvas.SetEnabled(fixture.Projection(blob), false);
        fixture.Main.OpenDebugWindowCommand.Execute(source);
        UiRuntime.Pump();
        var first = Assert.Single(System.Windows.Application.Current.Windows
            .OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>());
        UiRuntime.AwaitEditorReady(first);
        fixture.Main.OpenDebugWindowCommand.Execute(blob);
        UiRuntime.Pump();
        Assert.True(first.IsCurrent);
        Assert.Same(source, fixture.Main.SelectedNode);
        Assert.True(fixture.Projection(source).IsSelected);
        Assert.False(fixture.Projection(blob).IsSelected);

        fixture.Canvas.SetEnabled(fixture.Projection(blob), true);
        UiRuntime.Pump();
        bool? oldSelectionAtClose = null;
        first.Closing += (_, _) => oldSelectionAtClose = ReferenceEquals(source, fixture.Main.SelectedNode)
            && fixture.Projection(source).IsSelected && !fixture.Projection(blob).IsSelected;
        fixture.Main.OpenDebugWindowCommand.Execute(blob);
        UiRuntime.Pump();
        Assert.True(oldSelectionAtClose);
        Assert.False(first.IsCurrent);
        Assert.Same(blob, fixture.Main.SelectedNode);
        Assert.False(fixture.Projection(source).IsSelected);
        Assert.True(fixture.Projection(blob).IsSelected);
        var current = Assert.Single(System.Windows.Application.Current.Windows
            .OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>());
        UiRuntime.AwaitEditorReady(current);
        Assert.NotSame(first, current);
        UiRuntime.Await(current.CloseAndDrainAsync());
    });

    [Fact]
    public void Tool_parameter_windows_disable_node_menus_and_single_click_until_close() => UiRuntime.Run(() =>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 290);
        fixture.Connect(source, blob);
        fixture.Host.UpdateLayout();
        UiRuntime.Pump();
        Border HitArea(NodeDefinition node) => Assert.Single(UiRuntime.Visuals<Border>(fixture.View),
            border => border.Name == "HitAreaBorder" && ReferenceEquals(border.Tag, fixture.Projection(node)));

        foreach (var node in new[] { source, blob })
        {
            var other = ReferenceEquals(node, source) ? blob : source;
            Assert.NotNull(HitArea(node).ContextMenu);
            Assert.NotNull(HitArea(other).ContextMenu);
            fixture.Main.OpenDebugWindowCommand.Execute(node);
            UiRuntime.Pump();
            var window = Assert.Single(System.Windows.Application.Current.Windows
                .OfType<SunEyeVision.UI.Services.Editing.RuntimeToolEditorWindow>());
            UiRuntime.Await(window.AssemblyReadyTask);

            Assert.True(fixture.Main.IsDebugWindowOpen);
            Assert.Null(HitArea(node).ContextMenu);
            var otherHitArea = HitArea(other);
            Assert.Null(otherHitArea.ContextMenu);
            otherHitArea.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            Assert.Same(node, fixture.Main.SelectedNode);

            window.Close();
            UiRuntime.Pump();
            Assert.False(fixture.Main.IsDebugWindowOpen);
            Assert.NotNull(HitArea(node).ContextMenu);
            Assert.NotNull(HitArea(other).ContextMenu);
        }
    });

    [Fact]
    public void Image_source_and_blob_share_node_state_layers_with_their_own_outlines() => UiRuntime.Run(() =>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 290);
        fixture.ConfigureImage(source, fixture.Image());
        fixture.Connect(source, blob);
        fixture.Host.UpdateLayout();
        UiRuntime.Pump();

        var imageNode = fixture.Projection(source);
        var blobNode = fixture.Projection(blob);
        System.Windows.Shapes.Path Outline(CanvasNodeProjection node) =>
            Assert.Single(UiRuntime.Visuals<System.Windows.Shapes.Path>(fixture.View),
                path => path.Name == "NodeStatusBorder" && ReferenceEquals(path.DataContext, node));
        Canvas Flag(CanvasNodeProjection node) => Assert.Single(UiRuntime.Visuals<Canvas>(fixture.View),
            canvas => canvas.Name == "StateFlag" && ReferenceEquals(canvas.DataContext, node));

        var imageOutline = Outline(imageNode);
        var blobOutline = Outline(blobNode);
        Assert.False(imageOutline.Data.FillContains(new Point(5, 2)));
        Assert.True(blobOutline.Data.FillContains(new Point(5, 2)));
        Assert.Same(imageOutline.Data, Flag(imageNode).Clip);
        Assert.Same(blobOutline.Data, Flag(blobNode).Clip);

        fixture.Run();
        Assert.Equal(CanvasNodeRunState.Passed, imageNode.RunState);
        Assert.Equal(Visibility.Visible, Flag(imageNode).Visibility);
        fixture.Canvas.SetEnabled(imageNode, false);
        UiRuntime.Pump();
        imageNode = fixture.Projection(source);
        imageNode.IsSelected = false;
        Assert.Equal(Visibility.Collapsed, Flag(imageNode).Visibility);
        Assert.Null(Flag(imageNode).Background);
        Assert.Equal(Visibility.Visible, Outline(imageNode).Visibility);
        var disabledStroke = Outline(imageNode).Stroke;
        imageNode.IsSelected = true;
        Assert.Equal(Visibility.Visible, Outline(imageNode).Visibility);
        Assert.NotSame(disabledStroke, Outline(imageNode).Stroke);
    });

    [Fact]
    public void Blob_required_source_controls_the_canvas_border_through_connection_history()=>UiRuntime.Run(()=>
    {
        using var fixture = new DocumentUiFixture();
        var source = fixture.Add();
        var blob = fixture.Add("BlobAnalysis", 350, 290);
        var node = fixture.Projection(blob);
        node.IsSelected = false;
        var border = Assert.Single(UiRuntime.Visuals<System.Windows.Shapes.Path>(fixture.View),
            path => path.Name == "NodeStatusBorder" && ReferenceEquals(path.DataContext, node));
        Assert.True(node.HasUnconfiguredRequiredInputs);
        Assert.Equal(Visibility.Visible, border.Visibility);
        Assert.Equal(new[] { 3d, 2d }, border.StrokeDashArray);

        var connection = fixture.Connect(source, blob);
        Assert.False(node.HasUnconfiguredRequiredInputs);
        Assert.Equal(Visibility.Collapsed, border.Visibility);
        fixture.Canvas.Delete(Array.Empty<CanvasNodeProjection>(), new[] { connection });
        Assert.True(node.HasUnconfiguredRequiredInputs);
        Assert.Equal(Visibility.Visible, border.Visibility);

        Assert.True(fixture.Canvas.Undo());
        Assert.False(node.HasUnconfiguredRequiredInputs);
        Assert.Equal(Visibility.Collapsed, border.Visibility);
        Assert.True(fixture.Canvas.Redo());
        Assert.True(node.HasUnconfiguredRequiredInputs);
        node.IsSelected = true;
        Assert.Equal(new[] { 3d, 2d }, border.StrokeDashArray);
        Assert.Same(border.FindResource("Col_EditProcess_Modular_BorderBrush"), border.Stroke);
    });

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
