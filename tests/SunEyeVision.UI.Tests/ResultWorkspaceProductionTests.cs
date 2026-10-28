using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ResultWorkspaceProductionTests
{
    private static readonly CoordinateFrame Coordinates=SunEyeVision.TestFixtures.WorkspaceProbeDefinition.Coordinates;
    private static readonly ToolSchema ProbeSchema=SunEyeVision.TestFixtures.WorkspaceProbeDefinition.Schema;
    // Controlled neutral output for consumer/ownership tests; never an SDK tool producer.
    public sealed class Results
    {
        internal IReadOnlyDictionary<string, object?> Values = new Dictionary<string, object?>();
        internal IReadOnlyImage Image = null!;
        internal DeclaredOutputSnapshot Snapshot = null!;
        internal SunEyeVision.TestFixtures.WorkspaceProbeOutput Output=null!;
        internal int Releases=>Output.Disposals;
    }
    private static IReadOnlyList<OutputVariable> Contract(ToolSchema schema) => schema.Outputs.Select(port => new OutputVariable {
        PortId = port.Id, Name = port.Id, DisplayName = port.Id, ValueDeclaration = port.Type,
        DeclaredType = port.Type == TypeRef.ResourceOf(ResourceKind.Image) ? typeof(IReadOnlyImage)
            : port.Type == TypeRef.ResourceOf(ResourceKind.Region) ? typeof(IReadOnlyRegion) : port.Type.Shape == ValueShape.Geometry ? typeof(GeometryValue) : port.Type.Shape==ValueShape.Sequence?typeof(long[]):typeof(long),
        VariableType = port.Type == TypeRef.ResourceOf(ResourceKind.Image) ? "Image" : port.Type.Shape == ValueShape.Scalar ? "Integer" : "", Kind = OutputVariableKind.Data
    }).ToArray();
    internal sealed class Fixture : IDisposable
    {
        internal readonly string Directory;
        internal readonly Solution Document;
        internal readonly SunEyeVision.Workflow.Workflow Flow;
        internal readonly SunEyeVision.UI.Shared.WorkflowNode Source, Selected;
        internal readonly MainWindowViewModel Main;
        internal readonly ImageResultControl View;
        internal readonly ResultWorkspaceShell Shell;
        internal readonly Window Host;
        internal readonly WorkflowContext Context;
        internal readonly NodeResultManager Manager;
        internal readonly List<Results> Runs = new();
        private readonly object? _base = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        private bool _disposed;
        internal ResultWorkspacePresenter Presenter => Assert.IsType<ResultWorkspacePresenter>(typeof(ImageResultControl)
            .GetField("_workspacePresenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(View));
        internal Fixture()
        {
            if (Application.Current is null) _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var host = typeof(ResultWorkspaceProductionTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "HostDebugRoot").Value!;
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", Path.TrimEndingDirectorySeparator(Path.GetFullPath(host)) + Path.DirectorySeparatorChar);
            foreach (var path in new[] { "Shell/Resources/AppResources.xaml", "Resources/Styles/CommonStyleResources.xaml", "Themes/Dark/DarkTheme.xaml" })
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SunEyeVision.UI;component/" + path, UriKind.Relative) });
            Assert.True(PluginLoader.LoadPlugins());
            if(!ToolRegistry.HasRuntime("ResultWorkspaceProbe"))
            {
                var metadata=new ToolComponentMetadata("ResultWorkspace.Runtime.Tests","ResultWorkspaceProbe",ToolComponentRole.Runtime,FoundationContract.Major,FoundationContract.Minor);
                using var registration=ToolRegistry.PrepareRegistration(Array.Empty<Type>(),new[]{new ToolComponentRegistration(metadata,typeof(SunEyeVision.TestFixtures.WorkspaceProbeDefinition))}).EnterCommit();
                registration.Publish();
            }
            Directory = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? Path.Combine(host, "..", "..", "_validation", "ResultWorkspace"), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory); ServiceInitializer.InitializeServices(); ServiceInitializer.InitializeSolutionManager(Directory);
            Source = new("source", "实际图像源", "实际图像源", "ImageLoad") { RuntimeConfiguration = RuntimeToolConfiguration.Create("ImageLoad") };
            Selected = new("selected", "受控结果节点", "受控结果节点", "ResultWorkspaceProbe") {RuntimeConfiguration=RuntimeToolConfiguration.Create("ResultWorkspaceProbe")};
            Flow = new("workspace-flow", "工作区正式流程"); Flow.Nodes.Add(Source); Flow.Nodes.Add(Selected);
            Flow.Connections.Add(new WorkflowConnection("source-to-result", Source.Id, Selected.Id));
            Document = new Solution { Workflows = new() { Flow } }; ServiceInitializer.SolutionManager.SetCurrentSolution(Document, false);
            Main = new(); Assert.Equal(Flow.Id, Main.WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId);
            Assert.Same(Flow.Nodes, Main.WorkflowTabViewModel.SelectedTab.WorkflowNodes);
            Main.SelectedNode = Selected; Selected.IsSelected = true;
            Context = Assert.IsType<WorkflowContext>(typeof(MainWindowViewModel).GetProperty("CurrentWorkflowContext", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Main));
            Manager = Assert.IsType<NodeResultManager>(typeof(MainWindowViewModel).GetField("_nodeResultManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Main));
            Publish(Source, 21); Publish(Selected, 41);
            View = new ImageResultControl { DataContext = Main.ImageResult }; View.Initialize(Main);
            Host = new Window { Content = View, Width = 1000, Height = 850, Left = -10000, ShowActivated = false }; Host.Show(); Pump();
            Shell = Assert.IsType<ResultWorkspaceShell>(View.FindName("Workspace"));
            Shell.IsDoubleImage = true; Shell.SelectedResultTabIndex = 0; Pump();
            Assert.True(View.IsLoaded); Assert.Same(Shell.InputViewport, Presenter.InputPane.Viewport.View);
            Assert.Same(Shell.OutputViewport, Presenter.OutputPane.Viewport.View);
            Assert.NotNull(Main.CurrentResultPublication);
        }
        internal Results Publish(SunEyeVision.UI.Shared.WorkflowNode node, byte first, string? scope = null, ToolExecutionOwner? owner = null,bool updateContext=true)
        {
            var schema = node.RuntimeConfiguration is {} runtime ? new ToolSchema(Array.Empty<Port>(), runtime.Schema.Outputs) : ProbeSchema;
            var identity=(owner??Document.ExecutionOwner).Begin((scope??Flow.Id)+"/"+node.Id,Guid.NewGuid(),"5.0");
            var resources=new FrozenOutputResources();
            Results result;
            try
            {
                var frame=new ImageFrameAddress(Guid.NewGuid(),node.Id,identity.ToInvocationIdentity(node.Id));
                var domain=resources.CreateRegion(Coordinates,frame,new[]{new RegionRun(0,0,2),new RegionRun(1,0,2)});
                var image=resources.CreateImage(new ImageLayout(3,2,4,PixelLayout.Gray8),frame,domain,new byte[]{first,2,3,99,4,5,6,88});
                var region=resources.CreateRegion(Coordinates,frame,new[]{new RegionRun(0,0,0)});
                var values=schema.Outputs.ToDictionary(port=>port.Id,port=>port.Type==TypeRef.ResourceOf(ResourceKind.Image)?(object?)image
                    :port.Type==TypeRef.ResourceOf(ResourceKind.Region)?region:port.Type.Shape==ValueShape.Geometry?new PointGeometry(Coordinates,new(1,1))
                    :port.Type.Shape==ValueShape.Sequence?new long[]{1,2,3}:port.Type.Scalar==ScalarKind.Int64?port.Id=="output.width"?(long)image.Width:port.Id=="output.height"?(long)image.Height:1L:(object?)"probe",StringComparer.Ordinal);
                result=new Results{Image=image,Values=values,Output=new(values)};
            }
            catch{resources.Dispose();throw;}
            var fields=Contract(schema).ToDictionary(output=>output.PortId!,output=>new DeclaredOutputField(output.Name,output.DisplayName,
                output.VariableType,output.DeclaredType,output.FieldId,output.Group,true));
            var snapshot=DeclaredOutputSnapshot.Capture(schema,0,identity,node.Id,
                new(true,result.Output,BusinessJudgment.Pass,Array.Empty<Diagnostic>()),resources,0,fields,scope??Flow.Id);
            result.Snapshot=snapshot;
            Assert.Equal((owner??Document.ExecutionOwner).Id,snapshot.Identity.DocumentSessionId);
            Assert.Equal(node.Id,snapshot.Identity.NodeId);Runs.Add(result);
            if(updateContext)Manager.UpdateNodeResult(node,snapshot,refreshDisplay:ReferenceEquals(node,Main.SelectedNode),targetContext:Context);
            Pump();return result;
        }
        internal ResultWorkspaceImageSource Choice(string node, string port = "image") => Shell.OutputItems.Cast<ResultWorkspaceImageSource>()
            .Single(source => source.Reference.NodeId == node && source.Reference.PortId == port);
        internal Results LatestSelected => Runs.Last(run => run.Snapshot.Identity.NodeId == Selected.Id);
        internal void SelectOutput(string node, string? port = null)
        { Shell.SelectedOutputItem = Choice(node, port ?? (node == Source.Id ? "output.image" : "image")); Pump(); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            View.Shutdown(); Host.Content = null; Host.Close(); Main.CloseResultPresentation(); Context.ClearNodeResults();
            foreach (var run in Runs) run.Snapshot.Dispose();
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", _base);
        }
    }
    [Fact]
    public void Loaded_real_workspace_uses_same_publication_in_two_independent_panes_and_switches_only_selected_side()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var table = f.Main.CurrentResultPublication!; var input = f.Presenter.InputPane.CurrentFrame!;
        Assert.NotEqual(input.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        f.SelectOutput(f.Source.Id); Assert.Equal(input.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        Assert.Equal(f.Source.Id,f.Selected.SelectedDisplaySource!.SourceNodeId);
        using var left = f.Presenter.InputPane.BorrowCurrentImage(); using var right = f.Presenter.OutputPane.BorrowCurrentImage();
        Assert.NotNull(left); Assert.NotNull(right); Assert.NotSame(left, right); Assert.Equal(left.Image.Frame, right.Image.Frame);
        f.Shell.SelectedInputItem = null; Pump(); Assert.Null(f.Presenter.InputPane.CurrentFrame);
        Assert.False(left.IsCurrent); Assert.True(right.IsCurrent); Assert.Equal(new byte[] { 21 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
        f.Shell.SelectedInputItem = f.Shell.InputItems.Cast<ResultWorkspaceImageSource>().First(); Pump();
        var restored = f.Presenter.InputPane.CurrentFrame;
        f.SelectOutput(f.Selected.Id); Assert.Equal(restored, f.Presenter.InputPane.CurrentFrame);
        Assert.Equal(f.Selected.Id,f.Selected.SelectedDisplaySource!.SourceNodeId);
        Assert.NotEqual(restored!.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame); Assert.Same(table, f.Main.CurrentResultPublication); Assert.True(table.IsCurrent());
    });
    [Fact]
    public void Undeclared_region_resources_do_not_draw_and_same_frame_selection_rejects_old_publication()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var output = Assert.IsType<ImageViewBox>(f.Shell.OutputViewport);
        // A resource value alone is not a display declaration. Do not invent visible layers or a default color.
        Assert.Empty(RegionLayers(output)); Assert.Empty(RegionLayers(Assert.IsType<ImageViewBox>(f.Shell.InputViewport)));
        var old = f.Main.CurrentResultPublication!.Stamp; var oldFrame = f.LatestSelected.Image.Frame;
        f.Publish(f.Selected, 51); var publication = f.Main.CurrentResultPublication!; var currentImage = f.LatestSelected.Image;
        DeliverSelection(f.Main, new(publication.Stamp, "current", new[] { new ResultHighlightTarget(currentImage.Frame, Coordinates, new GeometryValue[] { new PointGeometry(Coordinates, new(1, 1)) }) })); Pump();
        var currentShapes = Assert.IsAssignableFrom<IList>(output.ShapeList); Assert.Single(currentShapes.Cast<object>()); var shape = currentShapes[0];
        DeliverSelection(f.Main, new(old, "late-old", new[] { new ResultHighlightTarget(oldFrame, Coordinates, new GeometryValue[] { new PointGeometry(Coordinates, new(0, 0)) }) })); Pump();
        Assert.Same(currentShapes, output.ShapeList); Assert.Same(shape, ((IList)output.ShapeList!)[0]);
        f.Main.ClearResultHighlight(); Pump(); Assert.True(output.ShapeList is null || ((IList)output.ShapeList).Count == 0); Assert.Empty(RegionLayers(output));
        f.SelectOutput(f.Source.Id); Assert.Empty(RegionLayers(output)); Assert.True(publication.IsCurrent());
    });
    [Fact]
    public void Original_shape_DP_reentry_installs_B_in_both_panes_and_old_A_cannot_clear_or_write_output()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); f.SelectOutput(f.Source.Id);
        var input = Assert.IsType<ImageViewBox>(f.Shell.InputViewport); var output = Assert.IsType<ImageViewBox>(f.Shell.OutputViewport);
        var publication = f.Main.CurrentResultPublication!;
        var receipt = f.Presenter.InputPane.CurrentFrame!;
        Assert.Equal(receipt.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        ResultSelection Selection(string name, double x) => new(publication.Stamp, name,
            new[] { new ResultHighlightTarget(receipt.Frame, receipt.Coordinates, new GeometryValue[] { new PointGeometry(receipt.Coordinates, new(x, 1)) }) });
        DeliverSelection(f.Main, Selection("initial", 0));
        Assert.Single(((IList)input.ShapeList!).Cast<object>()); Assert.Single(((IList)output.ShapeList!).Cast<object>());
        var armed = true; var delivered = false;
        object? inputB = null, outputB = null, inputShapeB = null, outputShapeB = null;
        var descriptor = DependencyPropertyDescriptor.FromProperty(ImageViewBox.ShapeListProperty, typeof(ImageViewBox));
        EventHandler changed = (_, _) =>
        {
            if (!armed || input.ShapeList is IList { Count: > 0 }) return;
            armed = false; delivered = true;
            // This is the real original ShapeList DP notification caused by A clearing its input layer.
            DeliverSelection(f.Main, Selection("B", 1));
        };
        descriptor.AddValueChanged(input, changed);
        try { DeliverSelection(f.Main, Selection("A-outer", 2)); }
        finally { descriptor.RemoveValueChanged(input, changed); }
        Assert.True(delivered);
        Pump();
        inputB=input.ShapeList;outputB=output.ShapeList;
        inputShapeB=Assert.Single(Assert.IsAssignableFrom<IList>(inputB).Cast<object>());
        outputShapeB=Assert.Single(Assert.IsAssignableFrom<IList>(outputB).Cast<object>());
        Assert.Equal(1,Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface.IPoint>(inputShapeB).Point.X);
        Assert.Equal(1,Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface.IPoint>(outputShapeB).Point.X);
        Assert.Same(inputB, input.ShapeList); Assert.Same(outputB, output.ShapeList);
        Assert.Same(inputShapeB, ((IList)input.ShapeList!)[0]); Assert.Same(outputShapeB, ((IList)output.ShapeList!)[0]);
        Assert.Same(publication, f.Main.CurrentResultPublication); Assert.True(publication.IsCurrent());
        Assert.Equal(receipt.Frame, f.Presenter.InputPane.CurrentFrame!.Frame);
        Assert.Equal(receipt.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
    });
    [Fact]
    public void Explicit_clear_removes_highlight_after_publication_dispose_even_when_Main_still_holds_it()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); f.SelectOutput(f.Source.Id);
        var publication = f.Main.CurrentResultPublication!; var receipt = f.Presenter.InputPane.CurrentFrame!;
        DeliverSelection(f.Main, new(publication.Stamp, "before-retirement", new[] {
            new ResultHighlightTarget(receipt.Frame, receipt.Coordinates, new GeometryValue[] { new PointGeometry(receipt.Coordinates, new(1, 1)) }) }));
        var input = Assert.IsType<ImageViewBox>(f.Shell.InputViewport); var output = Assert.IsType<ImageViewBox>(f.Shell.OutputViewport);
        Assert.Single(((IList)input.ShapeList!).Cast<object>()); Assert.Single(((IList)output.ShapeList!).Cast<object>());
        publication.Dispose(); Assert.False(publication.IsCurrent()); Assert.Same(publication, f.Main.CurrentResultPublication);
        f.Main.ClearResultHighlight(); Pump();
        Assert.True(input.ShapeList is null || ((IList)input.ShapeList).Count == 0);
        Assert.True(output.ShapeList is null || ((IList)output.ShapeList).Count == 0);
        Assert.Equal(receipt.Frame, f.Presenter.InputPane.CurrentFrame!.Frame);
        Assert.Equal(receipt.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
    });
    [Fact]
    public void New_run_notified_during_failed_refresh_observer_is_eventually_presented_after_exception()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();var before=f.Presenter.OutputPane.CurrentFrame!.Frame;
        int calls=0;Results? next=null;Action? changed=null;
        changed=()=>
        {
            f.Main.ResultWorkspaceChanged-=changed;calls++;
            next=f.Publish(f.Selected,81);
            throw new FormatException("受控刷新观察者失败；新run已在同一通知范围发布。");
        };
        f.Main.ResultWorkspaceChanged+=changed;
        try
        {
            Assert.Throws<FormatException>(()=>f.Main.RefreshNativeResultImages());
            Until(()=>next is not null&&f.Presenter.OutputPane.CurrentFrame?.Frame==next.Image.Frame);
            Assert.Equal(1,calls);Assert.NotNull(next);Assert.NotEqual(before,next.Image.Frame);
            Assert.Equal(new byte[]{81},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
            Assert.Equal(next.Snapshot.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);
            Assert.True(f.Main.CurrentResultPublication.IsCurrent());
        }
        finally{f.Main.ResultWorkspaceChanged-=changed;}
    });
    [Fact]
    public void Foreign_workflow_or_document_results_never_supply_display_images()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        f.Publish(f.Selected, 61, scope: "foreign-flow"); f.SelectOutput(f.Selected.Id); Assert.Null(f.Presenter.OutputPane.CurrentFrame);
        var foreign = new ToolExecutionOwner();
        try
        {
            f.Publish(f.Selected, 71, owner: foreign);
            f.SelectOutput(f.Selected.Id); Assert.Null(f.Presenter.OutputPane.CurrentFrame);
            Assert.NotNull(f.Presenter.InputPane.CurrentFrame); // The valid actual upstream input remains independent.
        }
        finally { foreign.Retire(); }
    });
    [Fact]
    public void Foreign_late_notification_without_context_replacement_keeps_current_publication_and_both_frames()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();var publication=f.Main.CurrentResultPublication;
        var input=f.Presenter.InputPane.CurrentFrame;var output=f.Presenter.OutputPane.CurrentFrame;
        var foreign=new ToolExecutionOwner();
        try
        {
            var rejected=f.Publish(f.Selected,91,owner:foreign,updateContext:false);
            f.Main.UpdateModuleResults(rejected.Snapshot,f.Selected.Name,f.Selected.ToolType,f.Selected.Id);Pump();
            Assert.Same(publication,f.Main.CurrentResultPublication);Assert.True(publication!.IsCurrent());
            Assert.Same(input,f.Presenter.InputPane.CurrentFrame);Assert.Same(output,f.Presenter.OutputPane.CurrentFrame);
        }
        finally{foreign.Retire();}
    });
    [Fact]
    public void Unload_reload_reuses_original_views_and_final_shutdown_drains_publication_owners()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var inputView = f.Shell.InputViewport; var outputView = f.Shell.OutputViewport;
        var firstPresenter = f.Presenter; var publication = f.Main.CurrentResultPublication!;
        using var pixels = f.LatestSelected.Image.AcquirePixels(); var frame = pixels.Frame;
        f.Host.Content = null; Pump(); Assert.False(f.View.IsLoaded);
        Assert.Null(firstPresenter.InputPane.CurrentFrame); Assert.Null(firstPresenter.OutputPane.CurrentFrame); Assert.True(publication.IsCurrent());
        f.Host.Content = f.View; Pump(); Assert.NotSame(firstPresenter, f.Presenter);
        Assert.Same(inputView, f.Shell.InputViewport); Assert.Same(outputView, f.Shell.OutputViewport);
        Assert.Equal(frame, f.Presenter.OutputPane.CurrentFrame!.Frame); Assert.Same(publication, f.Main.CurrentResultPublication);
        f.Dispose(); Assert.False(publication.IsCurrent()); Assert.All(f.Runs, run => Assert.Equal(1, run.Releases));
        Assert.Equal(41, pixels.GetRow(0)[0]); pixels.Dispose(); Assert.Throws<ObjectDisposedException>(() => pixels.GetRow(0).ToArray());
    });
    [Fact]
    public void Actual_file_selection_uses_preview_without_ROI_authority_and_clear_image_does_not_retire_table()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var file = Path.Combine(f.Directory, "preview.png");
        var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 91, 92, 0, 0 }, 4); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(file)) encoder.Save(stream);
        var host = ServiceInitializer.SolutionManager.Session!.Editing.Host;
        var draft = host.BeginEdit(f.Flow.Id, f.Source.Id);
        var policy = ImageSourcePolicy.Read(draft.Node)!; policy.FilePaths = new() { file }; policy.CurrentIndex = 0; policy.ApplyTo(draft.Node);
        host.CommitBatch(new[] { draft }, null);
        f.Main.SelectedNode = f.Source; f.Source.IsSelected = true; Pump();
        Until(() => f.Main.ActiveInputSource?.Images.Any(image => image.FilePath == file)==true);
        var stripSession=f.View.ImagePreviewContent.Session!;
        var selected=f.Main.ActiveInputSource!.Images.Single(image=>image.FilePath==file);
        var selection=stripSession.RequestAsync(new(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SelectStart,stripSession.Version,selected.Id),CancellationToken.None);
        Until(()=>selection.IsCompleted);selection.GetAwaiter().GetResult();
        Until(() => f.Presenter.OutputPane.CurrentFrame?.Frame.SourceId.StartsWith("local-preview:", StringComparison.Ordinal) == true);
        Assert.Equal(new byte[] { 91 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0)); Assert.Null(f.Presenter.OutputPane.BorrowCurrentImage());
        var table = f.Main.CurrentResultPublication; f.Main.ClearImageCommand.Execute(null); Pump();
        Assert.Null(f.Presenter.OutputPane.CurrentFrame); Assert.Same(table, f.Main.CurrentResultPublication); Assert.True(table!.IsCurrent());
    });
    [Fact]
    public void Showing_unchanged_formal_input_preserves_the_runtime_cursor_and_image_objects()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;
        var paths=new[]{Path.Combine(f.Directory,"first.png"),Path.Combine(f.Directory,"second.png")};
        foreach(var path in paths)
        {
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{5,0,0,0},4)));
            using var output=File.Create(path);png.Save(output);
        }
        source.AddImages(paths.Select(path=>new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}),selectFirstAdded:true);
        var second=source.Images[1];var version=source.EditVersion;
        Assert.True(source.TryPresentRuntimeCursor(version,second.Id,paths[1],paths[1]));
        Assert.Equal(0,source.CapturePolicy().CurrentIndex);Assert.Equal(1,source.CurrentIndex);
        f.Main.UpdateImagePreviewVisibility(f.Source);Pump();
        Assert.Same(source,f.Main.ActiveInputSource);Assert.Same(second,source.Images[1]);
        Assert.Equal(version,source.EditVersion);Assert.Equal(1,source.CurrentIndex);Assert.Equal(0,source.CapturePolicy().CurrentIndex);
    });
    [Fact]
    public void Obsolete_or_unknown_selector_items_restore_current_choice_without_clearing_new_frames()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        var oldInput = f.Shell.SelectedInputItem;
        var oldOutput = Assert.IsType<ResultWorkspaceImageSource>(f.Shell.SelectedOutputItem);
        f.Publish(f.Selected, 91);
        var currentInput = f.Shell.SelectedInputItem;
        var currentOutput = Assert.IsType<ResultWorkspaceImageSource>(f.Shell.SelectedOutputItem);
        Assert.NotSame(oldOutput, currentOutput); Assert.Equal(oldOutput.Key, currentOutput.Key);
        var inputFrame = f.Presenter.InputPane.CurrentFrame; var outputFrame = f.Presenter.OutputPane.CurrentFrame;
        Assert.NotNull(outputFrame); var publication = f.Main.CurrentResultPublication;
        f.Shell.SelectedOutputItem = oldOutput;
        Assert.Same(currentOutput, f.Shell.SelectedOutputItem);
        Assert.Equal(outputFrame, f.Presenter.OutputPane.CurrentFrame);
        f.Shell.SelectedInputItem = oldInput;
        Assert.Same(currentInput, f.Shell.SelectedInputItem);
        Assert.Equal(inputFrame, f.Presenter.InputPane.CurrentFrame);
        f.Shell.SelectedOutputItem = new object(); f.Shell.SelectedInputItem = new object();
        Assert.Same(currentOutput, f.Shell.SelectedOutputItem); Assert.Same(currentInput, f.Shell.SelectedInputItem);
        Assert.Equal(inputFrame, f.Presenter.InputPane.CurrentFrame); Assert.Equal(outputFrame, f.Presenter.OutputPane.CurrentFrame);
        Assert.Same(publication, f.Main.CurrentResultPublication); Assert.True(publication!.IsCurrent());
        Assert.Equal(new byte[] { 91 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
    });
    [Fact]
    public void Restoring_obsolete_input_choice_does_not_discard_a_synchronous_output_choice()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var oldInput = f.Shell.SelectedInputItem;
        f.Publish(f.Selected, 101); var currentInput = f.Shell.SelectedInputItem;
        var nextOutput = f.Choice(f.Source.Id, "output.image"); var delivered = false;
        var descriptor = DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedInputItemProperty, typeof(ResultWorkspaceShell));
        EventHandler changed = (_, _) =>
        {
            if (delivered || !ReferenceEquals(f.Shell.SelectedInputItem, currentInput)) return;
            delivered = true; f.Shell.SelectedOutputItem = nextOutput;
        };
        descriptor.AddValueChanged(f.Shell, changed);
        try { f.Shell.SelectedInputItem = oldInput; }
        finally { descriptor.RemoveValueChanged(f.Shell, changed); }
        Assert.True(delivered); Assert.NotNull(f.Presenter.OutputPane.CurrentFrame);
        Assert.Equal(f.Presenter.InputPane.CurrentFrame!.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        Assert.Equal(new byte[] { 21 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
        Assert.Equal(f.Source.Id, Assert.IsType<ResultWorkspaceImageSource>(f.Shell.SelectedOutputItem).Reference.NodeId);
    });
    [Fact]
    public void Refresh_input_DP_observer_selects_current_output_and_preserves_new_intent_and_frame()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var delivered = false; ResultWorkspaceImageSource? chosen = null;
        var descriptor = DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedInputItemProperty, typeof(ResultWorkspaceShell));
        EventHandler changed = (_, _) =>
        {
            if (delivered || f.Shell.SelectedInputItem is not ResultWorkspaceImageSource) return;
            chosen = f.Choice(f.Source.Id, "output.image"); delivered = true;
            f.Shell.SelectedOutputItem = chosen;
        };
        descriptor.AddValueChanged(f.Shell, changed);
        try { f.Main.RefreshNativeResultImages(); Pump(); }
        finally { descriptor.RemoveValueChanged(f.Shell, changed); }
        Assert.True(delivered); Assert.NotNull(chosen); Assert.Same(chosen, f.Shell.SelectedOutputItem);
        Assert.Equal(f.Presenter.InputPane.CurrentFrame!.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        Assert.Equal(new byte[] { 21 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
        Assert.True(f.Main.CurrentResultPublication!.IsCurrent());
    });
    [Fact]
    public void Restore_obsolete_output_DP_observer_clears_choice_and_physical_image_consistently()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var oldOutput = f.Shell.SelectedOutputItem;
        f.Publish(f.Selected, 111); var accepted = f.Shell.SelectedOutputItem;
        var inputFrame = f.Presenter.InputPane.CurrentFrame; var publication = f.Main.CurrentResultPublication;
        var delivered = false;
        var descriptor = DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedOutputItemProperty, typeof(ResultWorkspaceShell));
        EventHandler changed = (_, _) =>
        {
            if (delivered || !ReferenceEquals(f.Shell.SelectedOutputItem, accepted)) return;
            delivered = true; f.Shell.SelectedOutputItem = null;
        };
        descriptor.AddValueChanged(f.Shell, changed);
        try { f.Shell.SelectedOutputItem = oldOutput; }
        finally { descriptor.RemoveValueChanged(f.Shell, changed); }
        Assert.True(delivered); Assert.Null(f.Shell.SelectedOutputItem); Assert.Null(f.Presenter.OutputPane.CurrentFrame);
        Assert.Equal(inputFrame, f.Presenter.InputPane.CurrentFrame);
        Assert.Same(publication, f.Main.CurrentResultPublication); Assert.True(publication!.IsCurrent());
    });
    [Fact]
    public void Image_presentation_DP_exception_with_new_run_notification_eventually_displays_new_frame()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var output = Assert.IsType<ImageViewBox>(f.Shell.OutputViewport);
        var before = f.Presenter.OutputPane.CurrentFrame!.Frame; var delivered = false; Results? next = null;
        var descriptor = DependencyPropertyDescriptor.FromProperty(ImageViewBox.ImageDataSourceProperty, typeof(ImageViewBox));
        EventHandler changed = (_, _) =>
        {
            if (delivered || output.ImageDataSource is null) return;
            delivered = true; next = f.Publish(f.Selected, 121);
            throw new FormatException("受控原图像呈现DP观察者异常");
        };
        descriptor.AddValueChanged(output, changed);
        try
        {
            f.Main.RefreshNativeResultImages();
            Until(() => next is not null && f.Presenter.OutputPane.CurrentFrame?.Frame == next.Image.Frame);
            Assert.True(delivered); Assert.NotEqual(before, f.Presenter.OutputPane.CurrentFrame!.Frame);
            Assert.Equal(new byte[] { 121 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
            Assert.Equal(next!.Snapshot.Identity, f.Main.CurrentResultPublication!.Stamp.Invocation);
            Assert.True(f.Main.CurrentResultPublication.IsCurrent());
        }
        finally { descriptor.RemoveValueChanged(output, changed); }
    });
    [Fact]
    public void Final_shutdown_cannot_recreate_presenter_or_borrowed_content_after_a_real_reload()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var presenter = f.Presenter;
        f.View.Shutdown(); f.View.Shutdown();
        f.Host.Content = null; Pump(); f.Host.Content = f.View; Pump();
        Assert.Null(typeof(ImageResultControl).GetField("_workspacePresenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.View));
        Assert.Null(f.Shell.CurrentResultContent);
        Assert.Null(presenter.InputPane.CurrentFrame); Assert.Null(presenter.OutputPane.CurrentFrame);
        Assert.Throws<ObjectDisposedException>(() => f.View.Initialize(f.Main));
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Real_tree_paging_failure_retires_physical_images_or_preserves_a_synchronous_new_run(bool replace)
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var before = f.Main.CurrentResultPublication!;
        var items=f.Main.FlatResultRows.Single(row=>row.DisplayName=="items");
        var fired=false;Results? next=null;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler changed=(_,_)=>
        {
            if(fired)return;fired=true;
            if(replace)next=f.Publish(f.Selected,151);
            throw new FormatException("workspace-paging-failure");
        };
        f.Main.FlatResultRows.CollectionChanged+=changed;
        try
        {
            Assert.ThrowsAny<Exception>(()=>items.IsExpanded=true);
            Assert.True(fired);Assert.False(before.IsCurrent());
            if(replace)
            {
                Assert.NotNull(next);Assert.True(f.Main.CurrentResultPublication!.IsCurrent());
                Assert.Equal(next!.Image.Frame,f.Presenter.OutputPane.CurrentFrame!.Frame);
                Assert.NotNull(((ImageViewBox)f.Shell.InputViewport).ImageDataSource);
                Assert.NotNull(((ImageViewBox)f.Shell.OutputViewport).ImageDataSource);
                Assert.Equal(new byte[]{151},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
            }
            else
            {
                Assert.Null(f.Main.CurrentResultPublication);
                Assert.Null(((ImageViewBox)f.Shell.InputViewport).ImageDataSource);
                Assert.Null(((ImageViewBox)f.Shell.OutputViewport).ImageDataSource);
                Assert.Null(f.Presenter.InputPane.CurrentFrame);Assert.Null(f.Presenter.OutputPane.CurrentFrame);
            }
        }
        finally{f.Main.FlatResultRows.CollectionChanged-=changed;}
        f.Dispose();Assert.All(f.Runs,run=>Assert.Equal(1,run.Releases));
    });
    private static void DeliverSelection(MainWindowViewModel main, ResultSelection selection)
    {
        // Exercise the production subscription transport with a deliberately late immutable notification.
        // No second presenter, fabricated publication, private renderer call, or canvas edit is involved.
        var handlers = Assert.IsAssignableFrom<Action<ResultSelection?>>(typeof(MainWindowViewModel)
            .GetField("ResultWorkspaceSelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main));
        handlers(selection);
    }
    private static object[] RegionLayers(ImageViewBox view)
    {
        view.ApplyTemplate();view.UpdateLayout();
        var panel=Assert.IsAssignableFrom<Panel>(view.Template.FindName("ImageLayers",view));
        var baseImage=Assert.IsType<Image>(view.Template.FindName("sImage",view));Assert.Contains(baseImage,panel.Children.Cast<object>());
        return panel.Children.Cast<object>().Where(layer=>!ReferenceEquals(layer,baseImage)).ToArray();
    }
    private static void Pump()
    { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    private static void Until(Func<bool> ready)
    { var watch = Stopwatch.StartNew(); while (!ready()) { if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("真实文件preview尚未进入原结果工作区。"); Pump(); Thread.Yield(); } }
}
