using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Rendering;
using SunEyeVision.UI.Shared;
using Xunit;
using NodeBase = SunEyeVision.Workflow.WorkflowNodeBase;
using Connection = SunEyeVision.Workflow.WorkflowConnection;
namespace SunEyeVision.UI.Tests;
[Collection("VM canvas UI")]
public sealed class VmGestureTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)));
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Children<T>(child)) yield return nested; }
    }
    private static object? Call(object target, string method, params object[] args) => target.GetType().GetMethods(Private).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static object? Field(object target, string name) => target.GetType().GetField(name, Private)!.GetValue(target);
    private static WorkflowCanvasControl Create(out WorkflowNode node)
    {
        var control = new WorkflowCanvasControl { Width = 700, Height = 450 };
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.UI;component/Themes/Dark/DarkTheme.xaml", UriKind.Relative) });
        node = new WorkflowNode { Id = "a", ToolType = "ImageLoad", DispName = "图像源1", GlobalIndex = 2, Position = new Point(150, 90) };
        var target = new WorkflowNode { Id = "b", ToolType = "BlobAnalysis", DispName = "Blob分析1", GlobalIndex = 1, Position = new Point(350, 290) };
        var model = new WorkflowTabViewModel(); model.SetNodes(new ObservableCollection<NodeBase> { node, target });
        model.SetConnections(new ObservableCollection<Connection> { new() { Id = "wire", SourceNodeId = "a", TargetNodeId = "b", SourcePort = "BottomPort", TargetPort = "TopPort" } });
        control.DataContext = model; control.Measure(new Size(700, 450)); control.Arrange(new Rect(0, 0, 700, 450)); control.UpdateLayout();
        return control;
    }
    private static void Down(UIElement source) => source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
    [Fact] public void Transparent_two_pixel_wire_does_not_start_blank_canvas_gesture() => Sta(() =>
    {
        var control = Create(out _);
        var hit = Children<System.Windows.Shapes.Path>(control).First(p => p.StrokeThickness == 2 && p.Stroke is SolidColorBrush b && b.Color.A == 0 && p.DataContext is Connection);
        Down(hit);
        Assert.False((bool)Field(control, "_isPanning")!); Assert.False((bool)Field(control, "_isBoxSelecting")!);
        control.DataContext = null;
    });
    [Fact] public void Wire_rectangle_handle_is_not_blank_canvas() => Sta(() =>
    {
        var control = Create(out _); var canvas = (System.Windows.Controls.Canvas)control.FindName("WorkflowCanvas");
        var handle = new Rectangle { Width = 6, Height = 6 }; canvas.Children.Add(handle);
        ((List<Rectangle>)Field(control, "_vmWireHandles")!).Add(handle);
        Down(handle);
        Assert.False((bool)Field(control, "_isPanning")!); Assert.False((bool)Field(control, "_isBoxSelecting")!);
        control.DataContext = null;
    });
    [Fact] public void Inner_card_hover_scope_shows_all_four_sibling_ports_and_hides_them() => Sta(() =>
    {
        var control = Create(out var node); var card = Children<Border>(control).Single(b => b.Name == "NodeBorder" && ReferenceEquals(b.Tag, node));
        Call(control, "SetPortsVisibility", card, true);
        var ports = Children<VmExecutionPort>(control).Where(p => ReferenceEquals(p.DataContext, node)).ToArray();
        Assert.Equal(4, ports.Length); Assert.All(ports, p => Assert.Equal(Visibility.Visible, p.Visibility));
        Call(control, "SetPortsVisibility", card, false); Assert.All(ports, p => Assert.Equal(Visibility.Collapsed, p.Visibility));
        control.DataContext = null;
    });
    [Fact] public void Unmodified_blank_drag_arms_marquee_and_escape_reset_removes_it() => Sta(() =>
    {
        var control = Create(out _); var canvas = (System.Windows.Controls.Canvas)control.FindName("WorkflowCanvas"); Down(canvas);
        Assert.True((bool)Field(control, "_isBoxSelecting")!); Assert.False((bool)Field(control, "_isPanning")!);
        Call(control, "CancelVmCanvasGesture"); Assert.False((bool)Field(control, "_isBoxSelecting")!);
        control.DataContext = null;
    });
    [Fact] public void Node_preview_and_cancel_do_not_mutate_original_model() => Sta(() =>
    {
        var control = Create(out var node); var original = node.Position;
        var type = typeof(WorkflowCanvasControl).GetNestedType("VmNodeMoveSession", BindingFlags.NonPublic)!;
        var session = Activator.CreateInstance(type)!;
        type.GetProperty("Owner")!.SetValue(session, control.DataContext);
        type.GetProperty("Tab")!.SetValue(session, control.DataContext);
        type.GetProperty("Pointer")!.SetValue(session, new Point(150, 90));
        type.GetProperty("Token")!.SetValue(session, "test-token");
        type.GetProperty("Stamp")!.SetValue(session, "test-stamp");
        type.GetProperty("Before")!.SetValue(session, new Dictionary<WorkflowNode, Point> { [node] = original });
        typeof(WorkflowCanvasControl).GetField("_vmNodeMove", Private)!.SetValue(control, session);
        Call(control, "PreviewVmNodeMove", session, new Point(182, 133));
        Assert.Equal(original, node.Position);
        var after = (Dictionary<WorkflowNode, Point>)type.GetProperty("After")!.GetValue(session)!;
        Assert.NotEqual(original, after[node]);
        var ghosts = (List<FrameworkElement>)type.GetProperty("Ghosts")!.GetValue(session)!;
        var ghost = Assert.IsType<Rectangle>(Assert.Single(ghosts));
        var brush = Assert.IsType<VisualBrush>(ghost.Fill);
        Assert.Equal(Stretch.None, brush.Stretch); Assert.Equal(.7, brush.Opacity);
        var originalVisual = Assert.IsType<Border>(brush.Visual);
        Assert.Equal("HitAreaBorder", originalVisual.Name); Assert.Same(node, originalVisual.Tag);
        var bounds = VisualTreeHelper.GetDescendantBounds(originalVisual);
        Assert.Equal(bounds.Width, ghost.Width); Assert.Equal(bounds.Height, ghost.Height);
        Assert.Equal(after[node].X - 65 + bounds.X, System.Windows.Controls.Canvas.GetLeft(ghost));
        Assert.Equal(after[node].Y - 25 + bounds.Y, System.Windows.Controls.Canvas.GetTop(ghost));
        // 拖出后回到起点，不能沿用上次预览的旧落点。
        Call(control, "HideVmNodeGhosts");
        Call(control, "PreviewVmNodeMove", session, new Point(150, 90));
        Assert.Empty(after);
        Call(control, "CancelVmCanvasGesture");
        Assert.Equal(original, node.Position); Assert.Null(Field(control, "_vmNodeMove"));
        Assert.Empty((List<FrameworkElement>)type.GetProperty("Ghosts")!.GetValue(session)!);
        control.DataContext = null;
    });
    [Fact] public void Actual_canvas_connection_follows_move_undo_redo_without_drag_service() => Sta(() =>
    {
        var control = Create(out var node); var tab = (WorkflowTabViewModel)control.DataContext;
        var connection = tab.WorkflowConnections.Single();
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(node.BottomPortPosition, connection.PathPoints[0]);
        var before = node.Position; var after = new Point(210, 140);
        var command = new MoveNodesSnapshotCommand(new[] { (node, before, after) }, tab.WorkflowConnections);
        tab.CommandManager.Execute(command);
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(node.BottomPortPosition, connection.PathPoints[0]); Assert.Equal(new Point(210, 155), connection.PathPoints[0]);
        tab.CommandManager.Undo(); control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(new Point(150, 105), connection.PathPoints[0]);
        tab.CommandManager.Redo(); control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(new Point(210, 155), connection.PathPoints[0]);
        control.DataContext = null;
        Assert.Empty((HashSet<WorkflowNode>)Field(control, "_vmGeometrySubscriptions")!);
    });
    [Fact] public void Actual_canvas_retarget_uses_same_edge_and_refreshes_path_and_undo() => Sta(() =>
    {
        var control = Create(out var source); var tab = (WorkflowTabViewModel)control.DataContext;
        var edge = Assert.Single(tab.WorkflowConnections); var id = edge.Id;
        tab.CommandManager.Execute(new EditConnectionPortsCommand(edge, "RightPort", "LeftPort"));
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Same(edge, Assert.Single(tab.WorkflowConnections)); Assert.Equal(id, edge.Id);
        Assert.Equal(source.RightPortPosition, edge.PathPoints[0]);
        tab.CommandManager.Undo(); control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(source.BottomPortPosition, edge.PathPoints[0]);
        control.DataContext = null;
        Assert.Empty((HashSet<Connection>)Field(control, "_vmGeometryConnectionSubscriptions")!);
    });
    [Fact] public void Actual_hot_region_has_guides_and_respects_both_directions_of_bottom_port_occupancy() => Sta(() =>
    {
        var control = Create(out var node); var tab = (WorkflowTabViewModel)control.DataContext;
        Call(control, "UpdateVmPlacementFeedback", new Point(150, 160), 30d, Array.Empty<string>());
        Assert.NotEmpty((List<System.Windows.Shapes.Line>)Field(control, "_vmAlignmentGuides")!);
        Assert.NotNull(Field(control, "_vmNodeHotRegion"));
        Assert.Null(Call(control, "VmAvailableHotPredecessor")); // a 的底部已作源端口。
        tab.WorkflowConnections.Clear();
        Assert.Equal("a", Call(control, "VmAvailableHotPredecessor"));
        tab.WorkflowConnections.Add(new Connection { Id = "reverse", SourceNodeId = "b", TargetNodeId = "a", SourcePort = "TopPort", TargetPort = "BottomPort" });
        Assert.Null(Call(control, "VmAvailableHotPredecessor"));
        Call(control, "ClearVmPlacementFeedback"); Assert.Null(Field(control, "_vmNodeHotRegion"));
        Assert.Empty((List<System.Windows.Shapes.Line>)Field(control, "_vmAlignmentGuides")!);
        control.DataContext = null;
    });
    [Fact] public void Insertion_hover_uses_rendered_geometry_and_restores_normal_thickness() => Sta(() =>
    {
        var control = Create(out _); var tab = (WorkflowTabViewModel)control.DataContext;
        var edge = tab.WorkflowConnections.Single();
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var points = edge.PathPoints;
        var pair = Enumerable.Range(0, points.Count - 1).Select(i => (A: points[i], B: points[i+1])).OrderByDescending(p => (p.B-p.A).Length).First();
        var pointer = pair.A + (pair.B-pair.A) * .5;
        Assert.Same(edge, Call(control, "FindVmInsertionWire", pointer, Array.Empty<string>()));
        Call(control, "UpdateVmInsertionWire", pointer, Array.Empty<string>());
        var view = (System.Windows.Shapes.Path)Field(control, "_vmInsertionView")!;
        Assert.NotNull(view); Assert.Equal(10d, view.StrokeThickness);
        Call(control, "ClearVmInsertionWire"); Assert.Equal(2d, view.StrokeThickness);
        Assert.Null(Call(control, "FindVmInsertionWire", pointer, new[] { "a" }));
        control.DataContext = null;
    });
    [Fact] public void Move_redo_uses_saved_endpoint_not_current_position()
    {
        var a = new WorkflowNode { Position = new Point(80, 90) }; var b = new WorkflowNode { Position = new Point(180, 290) };
        var command = new MoveNodesSnapshotCommand(new[] { (a, a.Position, new Point(105, 115)), (b, b.Position, new Point(205, 315)) });
        var history = new SunEyeVision.UI.Features.WorkflowUI.Commands.CommandManager(new(), new()); history.Execute(command);
        Assert.Equal(new Point(105, 115), a.Position); history.Undo(); Assert.Equal(new Point(80, 90), a.Position);
        a.Position = new Point(999, 999); history.Redo(); Assert.Equal(new Point(105, 115), a.Position); Assert.Equal(new Point(205, 315), b.Position);
    }
    [Fact] public void Group_frame_alignment_updates_wires_and_undo_restores_positions() => Sta(() =>
    {
        var control = Create(out var a); var tab = (WorkflowTabViewModel)control.DataContext;
        var b = tab.WorkflowNodes.OfType<WorkflowNode>().Single(n => n.Id == "b");
        var oldA = a.Position; var oldB = b.Position;
        a.IsSelected = b.IsSelected = true;
        typeof(WorkflowCanvasControl).GetField("_vmBoxSelectionActive", Private)!.SetValue(control, true);
        Call(control, "DrawVmSelectionFrame");
        var frame = Assert.IsType<Rectangle>(Field(control, "_vmSelectionFrame"));
        Assert.Equal(330, frame.Width); Assert.Equal(250, frame.Height);
        Down(frame); Assert.False((bool)Field(control, "_isBoxSelecting")!); Assert.False((bool)Field(control, "_isPanning")!);
        Call(control, "AlignVmSelection", SunEyeVision.UI.Services.Interaction.VmNodeAlignment.Left);
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(a.Position.X, b.Position.X);
        Assert.Equal(b.TopPortPosition, tab.WorkflowConnections.Single().PathPoints.Last());
        tab.CommandManager.Undo(); control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(oldA, a.Position); Assert.Equal(oldB, b.Position);
        tab.CommandManager.Redo(); Assert.Equal(a.Position.X, b.Position.X);
        a.IsSelected = b.IsSelected = false;
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Null(Field(control, "_vmSelectionFrame"));
        control.DataContext = null; Assert.Null(Field(control, "_vmSelectionFrame"));
    });
    [Fact] public void Group_disable_changes_original_baml_fill_and_undo_restores_it() => Sta(() =>
    {
        var control = Create(out var a); var tab = (WorkflowTabViewModel)control.DataContext;
        a.IsSelected = true; int notifications = 0;
        a.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(a.IsEnabled)) notifications++; };
        Call(control, "SetVmSelectionEnabled", false);
        control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        var card = Children<Border>(control).Single(b => b.Name == "NodeBorder" && ReferenceEquals(b.Tag, a));
        Assert.False(a.IsEnabled); Assert.Equal(1, notifications);
        Assert.Equal(ColorConverter.ConvertFromString("#D3D3D3F5"), Assert.IsType<LinearGradientBrush>(card.Background).GradientStops[0].Color);
        tab.CommandManager.Undo(); control.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.True(a.IsEnabled); Assert.Equal(2, notifications);
        Assert.Equal(ColorConverter.ConvertFromString("#FAF7F5"), Assert.IsType<LinearGradientBrush>(card.Background).GradientStops[0].Color);
        control.DataContext = null;
    });

    [Theory]
    [InlineData(false, DragDropKeyStates.LeftMouseButton, DragDropEffects.Move, false, true)]
    [InlineData(false, DragDropKeyStates.ControlKey, DragDropEffects.Move, false, false)]
    [InlineData(true, DragDropKeyStates.ControlKey | DragDropKeyStates.LeftMouseButton, DragDropEffects.Copy, false, true)]
    [InlineData(true, DragDropKeyStates.None, DragDropEffects.Copy, true, false)]
    [InlineData(true, DragDropKeyStates.ControlKey, DragDropEffects.Move, true, false)]
    [InlineData(true, DragDropKeyStates.ControlKey, DragDropEffects.Copy, true, true)]
    [InlineData(true, DragDropKeyStates.ControlKey | DragDropKeyStates.ShiftKey, DragDropEffects.Copy, true, false)]
    public void Node_copy_drag_keeps_original_allowed_effect_and_control_contract(bool copy, DragDropKeyStates keys, DragDropEffects allowed, bool dropping, bool expected)
    {
        var method=typeof(WorkflowCanvasControl).GetMethod("VmNodeDropEffectAllowed", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, method.Invoke(null,new object[]{copy,keys,allowed,dropping}));
    }

    [Fact] public void Group_original_dimensions_match_visual_ports_router_and_live_loop_parameters() => Sta(() =>
    {
        var control = Create(out var node); var tab=(WorkflowTabViewModel)control.DataContext;
        node.ToolType="Subroutine";node.DispName="子程序1";
        var parameters=new SunEyeVision.Plugin.SDK.Execution.Parameters.SubroutineParameters();node.Parameters=parameters;
        control.Dispatcher.Invoke(() => { },System.Windows.Threading.DispatcherPriority.ContextIdle);control.UpdateLayout();
        Assert.Equal(40,node.NodeRect.Height);Assert.Equal(60,node.HitAreaHeight);
        var hit=Children<Border>(control).Single(b=>b.Name=="HitAreaBorder"&&ReferenceEquals(b.Tag,node));
        var card=Children<Border>(control).Single(b=>b.Name=="NodeBorder"&&ReferenceEquals(b.Tag,node));
        Assert.Equal(60,hit.ActualHeight);Assert.Equal(40,card.ActualHeight);Assert.Equal(110,card.ActualWidth);
        Assert.IsType<ImageBrush>(card.Background);Assert.Equal(new Thickness(0),card.BorderThickness);
        Call(control,"SetPortsVisibility",card,true);control.UpdateLayout();
        var ports=Children<VmExecutionPort>(control).Where(p=>ReferenceEquals(p.DataContext,node)).ToArray();
        var expected=new Dictionary<string,Point>{{"TopPort",node.TopPortPosition},{"BottomPort",node.BottomPortPosition},{"LeftPort",node.LeftPortPosition},{"RightPort",node.RightPortPosition}};
        var canvas=(System.Windows.Controls.Canvas)control.FindName("WorkflowCanvas");
        foreach(var port in ports)Assert.Equal(expected[(string)port.Tag],port.TranslatePoint(port.Anchor,canvas));
        var portService = new SunEyeVision.UI.Services.Interaction.PortPositionService(canvas);
        foreach(var port in ports) { port.Visibility=Visibility.Collapsed;Assert.Equal(expected[(string)port.Tag],portService.QueryPortPosition(node.Id,(string)port.Tag)); }
        Assert.Equal(node.BottomPortPosition,tab.WorkflowConnections.Single().PathPoints.First());
        var overlay=Children<Grid>(control).Single(g=>g.Name=="GroupLoopOverlay"&&ReferenceEquals(g.DataContext,node));
        Assert.Equal(Visibility.Collapsed,overlay.Visibility);
        parameters.LoopConfig.EnableLoop=true;control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(Visibility.Visible,overlay.Visibility);
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1400,900,192,192,PixelFormats.Pbgra32);bitmap.Render(control);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        var directory=Environment.GetEnvironmentVariable("S2_CASE_ROOT")!;System.IO.Directory.CreateDirectory(directory);
        using(var output=System.IO.File.Create(System.IO.Path.Combine(directory,"vm-group-template.png")))encoder.Save(output);
        var oldLoop=parameters.LoopConfig;parameters.LoopConfig=new();control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.Equal(Visibility.Collapsed,overlay.Visibility);oldLoop.EnableLoop=false;oldLoop.EnableLoop=true;Assert.False(node.IsGroupLoopEnabled);
        node.Parameters=new SunEyeVision.Plugin.SDK.Execution.Parameters.SubroutineParameters();parameters.LoopConfig.EnableLoop=true;Assert.False(node.IsGroupLoopEnabled);
        node.ToolType="ImageLoad";control.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);control.UpdateLayout();Assert.Equal(30,node.NodeRect.Height);Assert.Equal(30,card.ActualHeight);
        control.DataContext=null;
    });
    [Theory]
    [InlineData(true,false,"待运行","group_bg.png")]
    [InlineData(true,true,"待运行","group_bg_focus.png")]
    [InlineData(true,false,"OK","group_ok.png")]
    [InlineData(true,true,"成功","group_ok_selected.png")]
    [InlineData(true,false,"NG","group_ng.png")]
    [InlineData(true,true,"失败","group_ng_selected.png")]
    [InlineData(false,true,"成功","group_bg_forbidden.png")]
    public void Group_background_uses_original_image_and_state_precedence(bool enabled,bool selected,string status,string file) => Sta(() =>
    {
        var converter=new SunEyeVision.UI.Converters.Node.VmGroupBackgroundConverter();
        var brush=Assert.IsType<ImageBrush>(converter.Convert(new object[]{enabled,selected,status},typeof(Brush),null!,System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Stretch.None,brush.Stretch);Assert.True(brush.IsFrozen);
        Assert.EndsWith(file,Assert.IsType<System.Windows.Media.Imaging.BitmapImage>(brush.ImageSource).UriSource.OriginalString);
    });

}
[CollectionDefinition("VM canvas UI", DisableParallelization = true)]
public sealed class VmCanvasUiCollection { }
