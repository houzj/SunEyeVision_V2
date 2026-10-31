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
using SunEyeVision.Plugin.Wpf.Presenters.Display;
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
        internal readonly SunEyeVision.UI.Shared.WorkflowNode Source, Selected, Other;
        internal readonly MainWindowViewModel Main;
        internal readonly ImageResultControl View;
        internal readonly ResultWorkspaceShell Shell;
        internal readonly Window Host;
        internal readonly WorkflowContext Context;
        internal readonly NodeResultManager Manager;
        internal readonly List<Results> Runs = new();
        private readonly object? _base = AppContext.GetData("APP_CONTEXT_BASE_DIRECTORY");
        private bool _disposed;
        private readonly FieldInfo _solutionManagerField=typeof(ServiceInitializer).GetField("_solutionManager",BindingFlags.Static|BindingFlags.NonPublic)!;
        private object? _previousSolutionManager;
        internal ResultWorkspacePresenter Presenter => Assert.IsType<ResultWorkspacePresenter>(typeof(ImageResultControl)
            .GetField("_workspacePresenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(View));
        internal Fixture(bool includeOther=false)
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
            System.IO.Directory.CreateDirectory(Directory); ServiceInitializer.InitializeServices();
            _previousSolutionManager=_solutionManagerField.GetValue(null);
            _solutionManagerField.SetValue(null,new SolutionManager(Directory));
            Source = new("source", "实际图像源", "实际图像源", "ImageLoad") { RuntimeConfiguration = RuntimeToolConfiguration.Create("ImageLoad") };
            Selected = new("selected", "受控结果节点", "受控结果节点", "ResultWorkspaceProbe") {RuntimeConfiguration=RuntimeToolConfiguration.Create("ResultWorkspaceProbe")};
            Other = new("other", "无关结果节点", "无关结果节点", "ResultWorkspaceProbe") {RuntimeConfiguration=RuntimeToolConfiguration.Create("ResultWorkspaceProbe")};
            Flow = new("workspace-flow", "工作区正式流程"); Flow.Nodes.Add(Source); Flow.Nodes.Add(Selected);
            Selected.RuntimeConfiguration=Selected.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>
            {
                ["input.image"]=new ReferenceSetting(new OutputReference(Source.Id,"output.image",Source.RuntimeConfiguration!.Schema.Outputs.Single(port=>port.Id=="output.image").Type,new[]{"data"},scopeId:Flow.Id))
            });
            if(includeOther)Flow.Nodes.Add(Other);
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
        internal Results Publish(SunEyeVision.UI.Shared.WorkflowNode node, byte first, string? scope = null, ToolExecutionOwner? owner = null,bool updateContext=true,Func<bool>? admission=null)
        {
            var schema = node.RuntimeConfiguration is {} runtime ? new ToolSchema(Array.Empty<Port>(), runtime.Schema.Outputs,outputDisplay:runtime.Schema.OutputDisplay) : ProbeSchema;
            var identity=(owner??Document.ExecutionOwner).Begin((scope??Flow.Id)+"/"+node.Id,Guid.NewGuid(),"5.0",admission:admission);
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
                    :port.Type.Shape==ValueShape.Record?new RecordValue(port.Type,port.Type.Fields.ToDictionary(field=>field.Id,field=>field.Type==TypeRef.ResourceOf(ResourceKind.Image)?(object?)image:field.Type.Scalar==ScalarKind.Int64?(field.Id=="width"?(long)image.Width:(long)image.Height):(object?)image.Layout.Format.ToString(),StringComparer.Ordinal)):port.Type.Shape==ValueShape.Sequence?new long[]{1,2,3}:port.Type.Scalar==ScalarKind.Int64?1L:(object?)"probe",StringComparer.Ordinal);
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
            Main.ReleaseDocumentHost();
            foreach (var run in Runs) run.Snapshot.Dispose();
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", _base);
            _solutionManagerField.SetValue(null,_previousSolutionManager);
        }
    }
    [Fact]
    public void Real_input_directory_uses_only_bound_image_ports_and_keeps_output_check_state_independent()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        var input=Assert.Single(f.Shell.InputItems.Cast<ResultWorkspaceImageSource>());
        var output=f.Choice(f.Source.Id,"output.image");
        Assert.Equal(output.Key,input.Key);Assert.NotSame(output,input);
        var identityHash=output.GetHashCode();
        f.Shell.SelectedOutputItem=output;Pump();
        Assert.Equal(identityHash,output.GetHashCode());
        Assert.True(output.IsCheck);Assert.False(input.IsCheck);Assert.False(output.CanOverlay);
        Assert.Contains("未实现",output.OverlayUnavailableReason);Assert.Contains("未实现",output.BackgroundUnavailableReason);
        Assert.Equal(1,f.Shell.OutputItems.Cast<ResultWorkspaceImageSource>().Count(item=>item.IsCheck));
        f.Selected.RuntimeConfiguration=f.Selected.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string,ParameterSetting>
            {["input.image"]=new UnsetSetting()});
        f.Main.RefreshNativeResultImages();Pump();
        Assert.Empty(f.Shell.InputItems.Cast<ResultWorkspaceImageSource>());Assert.Null(f.Shell.SelectedInputItem);
        Assert.Contains(f.Shell.OutputItems.Cast<ResultWorkspaceImageSource>(),item=>item.Reference.NodeId==f.Source.Id);
        Assert.Equal(1,f.Shell.OutputItems.Cast<ResultWorkspaceImageSource>().Count(item=>item.IsCheck));
    });
    [Fact]
    public void Output_member_selection_preserves_formal_identity_and_never_replaces_a_missing_choice()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        var gray=f.Choice(f.Source.Id,"output.gray-image");
        f.Shell.SelectedOutputItem=gray;Pump();
        var persisted=f.Selected.SelectedDisplayReference;
        Assert.Equal(gray.Key,persisted);
        var reference=BindingReferenceSerializer.Deserialize(persisted!);
        Assert.Equal("output.gray-image",reference.PortId);Assert.Equal(new[]{"data"},reference.MemberFieldPath);
        var file=Path.Combine(f.Directory,"selected-image.sln");
        Assert.True(ServiceInitializer.SolutionManager.SaveSolution(file));
        var reopened=Assert.IsType<Solution>(ServiceInitializer.SolutionManager.LoadSolutionOnly(file));
        var reopenedNode=reopened.Workflows.Single(flow=>flow.Id==f.Flow.Id).Nodes.Single(node=>node.Id==f.Selected.Id);
        Assert.Equal(persisted,reopenedNode.SelectedDisplayReference);
        var fragment=WorkflowFragmentCopy.Prepare(f.Flow,new[]{f.Source.Id,f.Selected.Id},f.Flow.Id,
            id=>id==f.Flow.Id?f.Flow:null,(node,_)=>new WorkflowNodeBase("copy-"+node.Id,node.Name,node.DispName,node.ToolType));
        var copied=BindingReferenceSerializer.Deserialize(fragment.Nodes.Single(node=>node.Id=="copy-"+f.Selected.Id).SelectedDisplayReference!);
        Assert.Equal("copy-"+f.Source.Id,copied.NodeId);Assert.Equal(f.Flow.Id,copied.ScopeId);
        Assert.Equal(reference.PortId,copied.PortId);Assert.Equal(reference.MemberFieldPath,copied.MemberFieldPath);
        f.Main.RefreshNativeResultImages();Pump();
        Assert.Equal(persisted,Assert.IsType<ResultWorkspaceImageSource>(f.Shell.SelectedOutputItem).Key);
        f.Selected.SelectedDisplayReference=BindingReferenceSerializer.Serialize(new OutputReference(
            reference.NodeId,reference.PortId,reference.Type,new[]{"missing"},scopeId:reference.ScopeId,storage:reference.Storage));
        f.Main.RefreshNativeResultImages();Pump();
        Assert.Null(f.Shell.SelectedOutputItem);Assert.Null(f.Presenter.OutputPane.CurrentFrame);
        Assert.Equal(new[]{"missing"},BindingReferenceSerializer.Deserialize(f.Selected.SelectedDisplayReference).MemberFieldPath);
    });
    [Fact]
    public void Unexecuted_image_source_uses_declared_names_without_published_snapshot()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        Assert.True(f.Context.RemoveNodeResult(f.Source.Id));
        f.Main.RefreshNativeResultImages();Pump();
        Assert.Equal(f.Source.Name+" · 图像",f.Choice(f.Source.Id,"output.image").DisplayName);
        Assert.Equal(f.Source.Name+" · 灰度图像",f.Choice(f.Source.Id,"output.gray-image").DisplayName);
    });
    [Fact]
    public void Output_check_notifications_cannot_overwrite_newer_selection_and_clear_retires_checks()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();var image=f.Choice(f.Source.Id,"output.image");var gray=f.Choice(f.Source.Id,"output.gray-image");
        var selected=f.Choice(f.Selected.Id);f.Shell.SelectedOutputItem=image;Pump();
        var reentered=false;
        image.PropertyChanged+=(_,args)=>
        {
            if(args.PropertyName==nameof(ResultWorkspaceImageSource.IsCheck)&&!image.IsCheck&&!reentered)
            {reentered=true;f.Shell.SelectedOutputItem=selected;}
        };
        f.Shell.SelectedOutputItem=gray;Pump();
        Assert.True(reentered);Assert.Same(selected,f.Shell.SelectedOutputItem);
        Assert.True(selected.IsCheck);Assert.False(image.IsCheck);Assert.False(gray.IsCheck);
        f.Shell.SelectedOutputItem=null;Pump();
        Assert.All(f.Shell.OutputItems.Cast<ResultWorkspaceImageSource>(),item=>Assert.False(item.IsCheck));
    });
    [Fact]
    public void Source_selectors_use_tool_declared_names_and_full_tooltips_without_changing_port_identity()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        var image=f.Choice(f.Source.Id,"output.image");var gray=f.Choice(f.Source.Id,"output.gray-image");
        Assert.Equal(f.Source.Name+" · 图像",image.DisplayName);
        Assert.Equal(f.Source.Name+" · 灰度图像",gray.DisplayName);
        Assert.Equal("output.image",image.Reference.PortId);Assert.Equal("output.gray-image",gray.Reference.PortId);
        f.Shell.SelectedOutputItem=gray;Pump();
        var outputSelector=System.Windows.Media.VisualTreeHelper.GetChildrenCount(f.Shell);
        Assert.True(outputSelector>0);
        var selectors=VisualDescendants<ComboBox>(f.Shell).Where(combo=>ReferenceEquals(combo.ItemsSource,f.Shell.OutputItems)
            ||ReferenceEquals(combo.ItemsSource,f.Shell.InputItems)).ToArray();
        Assert.Equal(2,selectors.Length);
        var currentChoice=Assert.IsType<ResultWorkspaceImageSource>(f.Shell.SelectedOutputItem);
        Assert.Equal(gray.Key,currentChoice.Key);
        Assert.Same(currentChoice,selectors.Single(selector=>ReferenceEquals(selector.ItemsSource,f.Shell.OutputItems)).SelectedItem);
        foreach(var selector in selectors)
            Assert.Equal((selector.SelectedItem as ResultWorkspaceImageSource)?.DisplayName,selector.ToolTip);
        var output=selectors.Single(selector=>ReferenceEquals(selector.ItemsSource,f.Shell.OutputItems));
        void Snapshot(FrameworkElement view,string name)
        {
            view.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,name));png.Save(file);
        }
        Snapshot(output,"s2-source-selector-closed.png");
        output.IsDropDownOpen=true;Pump();
        var popup=Assert.IsType<System.Windows.Controls.Primitives.Popup>(output.Template.FindName("Popup",output));
        Snapshot(Assert.IsAssignableFrom<FrameworkElement>(popup.Child),"s2-source-selector-popup.png");
        output.IsDropDownOpen=false;Pump();
        Assert.Equal(gray.Reference.PortId,f.Choice(f.Source.Id,"output.gray-image").Reference.PortId);
    });
    [Fact]
    public void Real_workspace_crosshair_command_opens_existing_original_settings_and_retires_with_host()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        var command=Assert.IsAssignableFrom<System.Windows.Input.ICommand>(f.Shell.CrossLinesCommand);
        Assert.True(command.CanExecute(null));command.Execute(null);Pump();
        var window=Application.Current.Windows.OfType<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display.CrosshairSettingsWindow>().Single();
        Assert.True(window.IsVisible);
        var layer=Assert.IsType<NativeResultViewport.CrosshairLayer>(window.DataContext);
        Assert.True(layer.CrossDatas[0].IsVisible);
        Assert.Same(f.Presenter.OutputPane.Viewport.View,f.Shell.OutputViewport);
        f.View.Shutdown();Pump();
        Assert.False(window.IsVisible);Assert.Null(f.Shell.CrossLinesCommand);
    });
    [Fact]
    public void Original_dual_image_transition_clears_hidden_input_and_reselects_first_without_changing_output()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f=new Fixture();
        var output=f.Presenter.OutputPane.CurrentFrame;
        using var input=f.Presenter.InputPane.BorrowCurrentImage();
        f.Shell.IsDoubleImage=false;Pump();
        Assert.Null(f.Shell.SelectedInputItem);Assert.Null(f.Presenter.InputPane.CurrentFrame);
        Assert.False(input!.IsCurrent);Assert.Equal(output,f.Presenter.OutputPane.CurrentFrame);
        f.Shell.IsDoubleImage=true;Pump();
        Assert.Same(f.Shell.InputItems.Cast<ResultWorkspaceImageSource>().First(),f.Shell.SelectedInputItem);
        Assert.NotNull(f.Presenter.InputPane.CurrentFrame);Assert.Equal(output,f.Presenter.OutputPane.CurrentFrame);
    });
    [Fact]
    public void Loaded_real_workspace_uses_same_publication_in_two_independent_panes_and_switches_only_selected_side()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture(); var table = f.Main.CurrentResultPublication!; var input = f.Presenter.InputPane.CurrentFrame!;
        Assert.NotEqual(input.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        f.SelectOutput(f.Source.Id); Assert.Equal(input.Frame, f.Presenter.OutputPane.CurrentFrame!.Frame);
        Assert.Equal(f.Source.Id,BindingReferenceSerializer.Deserialize(f.Selected.SelectedDisplayReference!).NodeId);
        using var left = f.Presenter.InputPane.BorrowCurrentImage(); using var right = f.Presenter.OutputPane.BorrowCurrentImage();
        Assert.NotNull(left); Assert.NotNull(right); Assert.NotSame(left, right); Assert.Equal(left.Image.Frame, right.Image.Frame);
        f.Shell.SelectedInputItem = null; Pump(); Assert.Null(f.Presenter.InputPane.CurrentFrame);
        Assert.False(left.IsCurrent); Assert.True(right.IsCurrent); Assert.Equal(new byte[] { 21 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0));
        f.Shell.SelectedInputItem = f.Shell.InputItems.Cast<ResultWorkspaceImageSource>().First(); Pump();
        var restored = f.Presenter.InputPane.CurrentFrame;
        f.SelectOutput(f.Selected.Id); Assert.Equal(restored, f.Presenter.InputPane.CurrentFrame);
        Assert.Equal(f.Selected.Id,BindingReferenceSerializer.Deserialize(f.Selected.SelectedDisplayReference!).NodeId);
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
    public void New_run_notified_during_failed_capture_is_eventually_presented_after_the_exception()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();var before=f.Presenter.OutputPane.CurrentFrame!.Frame;
        var previous=f.LatestSelected;previous.Snapshot.Dispose();
        var armed=false;var enteredCapture=false;var calls=0;Results? next=null;
        bool Admission()
        {
            if(!armed)return true;
            // This is the real upstream invocation gate, not a ResultWorkspaceChanged observer.
            // Only inject while the actual source-capture stack owns the admission check.
            if(!Environment.StackTrace.Contains("CaptureNativeImageSources",StringComparison.Ordinal))return true;
            armed=false;enteredCapture=true;calls++;
            next=f.Publish(f.Selected,82);
            throw new FormatException("受控上游身份读取失败；新运行已在真实来源捕获期间发布。");
        }
        var source=f.Publish(f.Source,22,admission:Admission);source.Snapshot.Dispose();
        armed=true;
        try
        {
            f.Main.RefreshNativeResultImages();
            Until(()=>next is not null&&f.Presenter.OutputPane.CurrentFrame?.Frame==next.Image.Frame);
            Assert.True(enteredCapture);Assert.Equal(1,calls);Assert.NotNull(next);
            Assert.NotEqual(before,next!.Image.Frame);Assert.Equal(new byte[]{82},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
            Assert.Equal(next.Snapshot.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);
            Assert.True(f.Main.CurrentResultPublication.IsCurrent());
            Assert.Equal(1,previous.Releases);Assert.Equal(0,source.Releases);
            f.Dispose();Assert.All(f.Runs,run=>Assert.Equal(1,run.Releases));
        }
        finally{armed=false;}
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
    [Theory]
    [InlineData("document")] [InlineData("workflow")] [InlineData("instance")]
    public void Foreign_owner_workflow_or_recreated_node_never_borrows_the_rejected_snapshot(string mismatch)
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        var owner = mismatch == "document" ? new ToolExecutionOwner() : null;
        try
        {
            var rejected = f.Publish(f.Selected, 93, scope: mismatch == "workflow" ? "foreign-flow" : null,
                owner: owner, updateContext: false);
            if (mismatch == "instance")
            {
                var index = f.Flow.Nodes.IndexOf(f.Selected);
                f.Flow.Nodes.RemoveAt(index);
                f.Flow.Nodes.Insert(index, new SunEyeVision.UI.Shared.WorkflowNode(f.Selected.Id, "替代节点", "替代节点", "ResultWorkspaceProbe")
                { RuntimeConfiguration = RuntimeToolConfiguration.Create("ResultWorkspaceProbe") });
            }
            f.Main.UpdateModuleResults(rejected.Snapshot, f.Selected.Name, f.Selected.ToolType, f.Selected.Id);
            Pump();
            Assert.NotEqual(rejected.Snapshot.Identity, f.Main.CurrentResultPublication?.Stamp.Invocation);
            rejected.Snapshot.Dispose();
            Assert.Equal(1, rejected.Releases);
        }
        finally { owner?.Retire(); }
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
        Until(()=>selection.IsCompleted);AwaitWorkspace(selection);
        Until(() => f.Presenter.OutputPane.CurrentFrame?.Frame.SourceId.StartsWith("local-preview:", StringComparison.Ordinal) == true);
        Assert.Equal("preview.png",Assert.IsType<ImageViewBox>(f.Shell.OutputViewport).ImageName);
        Assert.Equal(new byte[] { 91 }, f.Presenter.OutputPane.Viewport.ReadPixel(0, 0)); Assert.Null(f.Presenter.OutputPane.BorrowCurrentImage());
        var table = f.Main.CurrentResultPublication; f.Main.ClearImageCommand.Execute(null); Pump();
        Assert.Null(f.Presenter.OutputPane.CurrentFrame); Assert.Same(table, f.Main.CurrentResultPublication); Assert.True(table!.IsCurrent());
        Assert.Null(Assert.IsType<ImageViewBox>(f.Shell.OutputViewport).ImageName);
    });
    [Fact]
    public void Accepted_source_edit_with_failed_cursor_reset_is_visible_but_cannot_run_until_repaired()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=Assert.IsType<SunEyeVision.UI.Shared.ImageInputSource>(f.Main.ActiveInputSource);
        var a=new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(f.Directory,"a.png")};
        var b=new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(f.Directory,"b.png")};
        source.AddImages(new[]{a,b},selectFirstAdded:true);Pump();
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        var counter=typeof(WorkflowExecutionManager).GetField("_nextLocalImageReset",BindingFlags.Instance|BindingFlags.NonPublic)!;
        counter.SetValue(manager,long.MaxValue);
        var receipt=source.SelectImage(1);
        Assert.True(receipt.FormalAccepted);Assert.False(receipt.RuntimeReady);Assert.NotNull(receipt.ExecutionError);
        Assert.Same(b,source.CurrentImage);Assert.Equal(1,ImageSourcePolicy.Read(f.Source)!.CurrentIndex);
        Assert.Contains("运行游标不可用",f.Main.StatusText);
        var tab=f.Main.WorkflowTabViewModel.SelectedTab!;
        Assert.False(f.Main.TrySelectPreviewImage(tab,source,a,0,source.EditVersion));
        Assert.Equal(1,ImageSourcePolicy.Read(f.Source)!.CurrentIndex);
        var errors=new List<string>();manager.WorkflowExecutionError+=(_,e)=>errors.Add(e.ErrorMessage);
        var run=manager.RunSingleAsync(tab);Until(()=>run.IsCompleted);AwaitWorkspace(run);
        Assert.Contains(errors,error=>error.Contains("运行游标不可用"));
        counter.SetValue(manager,0L);
        var repaired=source.SelectImage(1);
        Assert.True(repaired.FormalAccepted);Assert.True(repaired.RuntimeReady);
        errors.Clear();var retry=manager.RunSingleAsync(tab,advanceLocalImage:false,explicitSourceId:source.NodeId);
        Until(()=>retry.IsCompleted);AwaitWorkspace(retry);
        Assert.DoesNotContain(errors,error=>error.Contains("运行游标不可用"));
    });
    [Fact]
    public void Actual_source_selection_reentry_retains_the_committed_new_choice_and_rejects_the_stale_run()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=Assert.IsType<SunEyeVision.UI.Shared.ImageInputSource>(f.Main.ActiveInputSource);
        var a=new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(f.Directory,"a.png")};
        var b=new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(f.Directory,"b.png")};
        source.AddImages(new[]{a,b},selectFirstAdded:true);Pump();
        EventHandler<int> selectAgain=(_,index)=>
        {if(index==0)f.Main.TrySelectPreviewImage(f.Main.WorkflowTabViewModel.SelectedTab!,source,b,1,source.EditVersion);};
        source.CurrentImageChanged+=selectAgain;
        try
        {
            var tab=f.Main.WorkflowTabViewModel.SelectedTab!;
            Assert.False(f.Main.TrySelectPreviewImage(tab,source,a,0,source.EditVersion));
            Assert.Same(b,source.CurrentImage);Assert.Equal(0,ImageSourcePolicy.Read(f.Source)!.CurrentIndex);
        }
        finally{source.CurrentImageChanged-=selectAgain;}
    });
    [Fact]
    public void Actual_result_workspace_navigation_commands_follow_formal_source_availability()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var previous=Assert.IsAssignableFrom<System.Windows.Input.ICommand>(f.Shell.PreviousImageCommand);
        var next=Assert.IsAssignableFrom<System.Windows.Input.ICommand>(f.Shell.NextImageCommand);
        Assert.False(previous.CanExecute(null));Assert.False(next.CanExecute(null));
        var source=Assert.IsType<SunEyeVision.UI.Shared.ImageInputSource>(f.Main.ActiveInputSource);
        var path=Path.Combine(f.Directory,"navigation.png");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{12,0,0,0},4)));
        using(var file=File.Create(path))encoder.Save(file);
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path}},selectFirstAdded:true);Pump();
        Assert.True(previous.CanExecute(null));Assert.True(next.CanExecute(null));
    });
    [Fact]
    public void Actual_folder_import_commits_once_and_round_trips_through_document_history()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var folder=Path.Combine(f.Directory,"folder");var child=Path.Combine(folder,"sub");System.IO.Directory.CreateDirectory(child);
        var first=Path.Combine(folder,"a.png");var second=Path.Combine(child,"b.png");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{12,0,0,0},4)));
        using(var file=File.Create(first))encoder.Save(file);File.Copy(first,second);
        var source=Assert.IsType<SunEyeVision.UI.Shared.ImageInputSource>(f.Main.ActiveInputSource);
        var strip=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        var history=ServiceInitializer.SolutionManager.Session!.History;var position=history.Position;
        var request=strip.RequestAsync(new(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.ImportDirectory,
            strip.Version,paths:new[]{folder}),CancellationToken.None);
        Until(()=>request.IsCompleted);AwaitWorkspace(request);Pump();
        Assert.Equal(position+1,history.Position);Assert.Equal(2,ImageSourcePolicy.Read(f.Source)!.FilePaths.Count);
        Assert.Equal(2,source.Count);Assert.Contains(source.Images,image=>image.FilePath==first);Assert.Contains(source.Images,image=>image.FilePath==second);
        history.Undo();Pump();Assert.Empty(ImageSourcePolicy.Read(f.Source)!.FilePaths);Assert.Empty(source.Images);
        history.Redo();Pump();Assert.Equal(2,ImageSourcePolicy.Read(f.Source)!.FilePaths.Count);Assert.Equal(2,source.Count);
        Assert.True(File.Exists(first));Assert.True(File.Exists(second));
    });
    [Fact]
    public void Real_result_host_keeps_the_horizontal_scrollbar_accessible_for_1051_images_and_releases_the_session()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        void Stage(string name){Console.WriteLine("1051_STAGE "+name);Console.Out.Flush();}
        Stage("fixture");using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();Stage("fixture-ready");
        var path=Path.Combine(f.Directory,"large-strip.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{12,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        var source=f.Main.ActiveInputSource!;
        Stage("add-batch");source.AddImages(Enumerable.Range(0,1051).Select(index=>new SunEyeVision.UI.Shared.ImageInfo
            {FilePath=path,SourceReference=path,Name="图像 "+index}),selectFirstAdded:true);
        Stage("batch-accepted");var strip=f.View.ImagePreviewContent;
        Until(()=>strip.IsLoaded&&strip.Items.Count==1051);
        Stage("layout");f.Host.UpdateLayout();Pump();Stage("layout-ready");
        var list=Assert.IsType<ListBox>(strip.FindName("lst"));
        var scroll=Assert.Single(VisualDescendants<ScrollViewer>(list));scroll.ApplyTemplate();scroll.UpdateLayout();
        var bar=Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(scroll.Template.FindName("PART_HorizontalScrollBar",scroll));
        Assert.Equal(Visibility.Visible,scroll.ComputedHorizontalScrollBarVisibility);Assert.True(bar.IsVisible);
        Assert.True(scroll.ScrollableWidth>0);Assert.True(bar.ActualHeight>0);
        var bounds=bar.TransformToAncestor(f.Shell).TransformBounds(new Rect(bar.RenderSize));
        var visible=bounds;
        for(DependencyObject? ancestor=VisualTreeHelper.GetParent(bar);ancestor is not null&&!ReferenceEquals(ancestor,f.Shell);ancestor=VisualTreeHelper.GetParent(ancestor))
            if(ancestor is FrameworkElement element)
                { var ancestorBounds=element.TransformToAncestor(f.Shell).TransformBounds(new Rect(element.RenderSize)); if(ancestorBounds.Top>bounds.Top||ancestorBounds.Bottom<bounds.Bottom) Console.WriteLine($"1051_CLIP {element.GetType().Name} {element.Name}: {ancestorBounds}; bar={bounds}; desired={element.DesiredSize}"); visible.Intersect(ancestorBounds); }
        Assert.True(visible.Height>=bounds.Height-0.5,"水平滚动条被结果区某级宿主裁切。");
        scroll.ScrollToHorizontalOffset(scroll.ScrollableWidth);Until(()=>scroll.HorizontalOffset>0);Pump();
        Assert.InRange(Enumerable.Range(0,list.Items.Count).Count(index=>list.ItemContainerGenerator.ContainerFromIndex(index) is not null),1,200);
        var session=Assert.IsType<ImageSourceStripSession>(strip.Session);var last=source.Images.Last();
        Stage("select-last");AwaitWorkspace(session.RequestAsync(new(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SelectStart,session.Version,last.Id),CancellationToken.None));Stage("selected-last");
        Pump();Assert.Same(last,source.CurrentImage);Assert.Equal(1050,source.CapturePolicy().CurrentIndex);
        strip.IsExpanded=false;Pump();Assert.Equal(40,strip.ActualHeight);Assert.False(bar.IsVisible);
        var visibleIds=typeof(ImageSourceStripSession).GetField("_visibleIds",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Until(()=>Assert.IsType<HashSet<string>>(visibleIds.GetValue(session)).Count==0);
        strip.IsExpanded=true;Pump();Assert.Equal(125,strip.ActualHeight);
        Stage("unload");f.Host.Content=null;Pump();Assert.Null(strip.Session);Assert.True(session.IsReadOnly);Assert.Empty(session.Items);Stage("unloaded");
        f.Host.Content=f.View;Pump();Until(()=>strip.Session?.Items.Count==1051);
        Assert.NotSame(session,strip.Session);Assert.Same(last,source.CurrentImage);
    });
    [Fact]
    public void Accepted_image_source_edit_retires_its_run_and_restores_formal_output_declaration()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var run=f.Publish(f.Source,33);var previous=f.Main.CurrentResultPublication;
        Assert.NotNull(previous);Assert.Equal(ResultViewState.Success,previous!.State);
        Assert.Contains(f.Main.FlatResultRows,row=>row.DisplayName=="模块状态"&&row.DisplayValue=="成功");
        Assert.Contains(f.Main.FlatResultRows,row=>row.DisplayName=="耗时（ms）"&&!string.IsNullOrWhiteSpace(row.DisplayValue));
        Assert.Same(run.Snapshot,f.Context.GetNodeResult(f.Source.Id)!.Result);
        var source=f.Main.ActiveInputSource!;
        var path=Path.Combine(f.Directory,"accepted-image.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{5,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);
        Pump();
        Assert.Null(f.Context.GetNodeResult(f.Source.Id));Assert.False(previous.IsCurrent());
        Assert.False(run.Snapshot.ExecutionIdentity!.IsCurrent);
        var declaration=Assert.IsAssignableFrom<IResultPublication>(f.Main.CurrentResultPublication);
        Assert.Equal(ResultViewState.Declaration,declaration.State);Assert.Null(declaration.Stamp.Invocation);
        Assert.Contains(declaration.Schema.Outputs,port=>port.Id=="output.image");
        Assert.NotEmpty(f.Main.FlatResultRows);
    });
    [Fact]
    public void Image_source_undo_and_redo_retire_the_affected_run_and_restore_its_declaration()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture(includeOther:true);f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;
        var path=Path.Combine(f.Directory,"history-image.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{8,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);
        var history=ServiceInitializer.SolutionManager.Session!.History;
        Assert.True(history.CanUndo);
        var unrelated=f.Publish(f.Other,39);Assert.Same(unrelated.Snapshot,f.Context.GetNodeResult(f.Other.Id)!.Result);
        var downstreamUndo=f.Publish(f.Selected,44);downstreamUndo.Snapshot.Dispose();
        var beforeUndo=f.Publish(f.Source,42);var undoPublication=f.Main.CurrentResultPublication!;
        Assert.Equal(ResultViewState.Success,undoPublication.State);
        beforeUndo.Snapshot.Dispose();Assert.Equal(0,beforeUndo.Releases);
        Assert.True(history.Undo());Pump();Assert.Null(history.LastObserverError);
        Assert.False(beforeUndo.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Empty(ImageSourcePolicy.Read(f.Source)!.FilePaths);
        Assert.Null(f.Context.GetNodeResult(f.Source.Id));Assert.False(undoPublication.IsCurrent());
        Assert.Null(f.Context.GetNodeResult(f.Selected.Id));Assert.Equal(1,downstreamUndo.Releases);
        Assert.Same(unrelated.Snapshot,f.Context.GetNodeResult(f.Other.Id)!.Result);
        Assert.Equal(ResultViewState.Declaration,Assert.IsAssignableFrom<IResultPublication>(f.Main.CurrentResultPublication).State);
        Assert.Equal(1,beforeUndo.Releases);

        var downstreamRedo=f.Publish(f.Selected,45);downstreamRedo.Snapshot.Dispose();
        var beforeRedo=f.Publish(f.Source,43);var redoPublication=f.Main.CurrentResultPublication!;
        Assert.Equal(ResultViewState.Success,redoPublication.State);
        beforeRedo.Snapshot.Dispose();Assert.Equal(0,beforeRedo.Releases);
        Assert.True(history.Redo());Pump();Assert.Null(history.LastObserverError);
        Assert.False(beforeRedo.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Equal(path,Assert.Single(ImageSourcePolicy.Read(f.Source)!.FilePaths));
        Assert.Null(f.Context.GetNodeResult(f.Source.Id));Assert.False(redoPublication.IsCurrent());
        Assert.Null(f.Context.GetNodeResult(f.Selected.Id));Assert.Equal(1,downstreamRedo.Releases);
        Assert.Same(unrelated.Snapshot,f.Context.GetNodeResult(f.Other.Id)!.Result);
        Assert.Equal(ResultViewState.Declaration,Assert.IsAssignableFrom<IResultPublication>(f.Main.CurrentResultPublication).State);
        Assert.Equal(1,beforeRedo.Releases);
    });
    [Fact]
    public void History_retirement_does_not_remove_a_new_run_published_by_a_reentrant_observer()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var path=Path.Combine(f.Directory,"reentrant-history.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{6,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);
        var old=f.Publish(f.Source,54);Results? replacement=null;
        void Reenter(string domain)
        {
            if(replacement is null && domain.Contains("/"+f.Flow.Id+"/"+f.Source.Id+"/",StringComparison.Ordinal))
                replacement=f.Publish(f.Source,55);
        }
        f.Document.ExecutionOwner.DomainRetired+=Reenter;
        try { Assert.True(ServiceInitializer.SolutionManager.Session!.History.Undo());Pump(); }
        finally { f.Document.ExecutionOwner.DomainRetired-=Reenter; }
        Assert.NotNull(replacement);Assert.False(old.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.True(replacement!.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Same(replacement.Snapshot,f.Context.GetNodeResult(f.Source.Id)!.Result);
        Assert.Equal(ResultViewState.Success,f.Main.CurrentResultPublication!.State);
    });
    [Fact]
    public void History_retirement_excludes_all_real_execution_admission_paths_until_every_node_is_invalidated()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var path=Path.Combine(f.Directory,"admission-history.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{7,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);
        var source=f.Publish(f.Source,71);var downstream=f.Publish(f.Selected,72);
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        var tab=f.Main.WorkflowTabViewModel.SelectedTab!;var attempted=false;var invoked=false;
        void Reenter(string domain)
        {
            if(attempted||!domain.Contains("/"+f.Flow.Id+"/"+f.Source.Id+"/",StringComparison.Ordinal))return;
            attempted=true;
            AwaitWorkspace(manager.RunSingleAsync(tab));Assert.False(manager.IsAnyRunning());
            Assert.False(manager.StartContinuousRun(tab));Assert.False(manager.IsAnyRunning());
            Assert.Throws<InvalidOperationException>(()=>AwaitWorkspace(manager.RunSolutionAsync(f.Main.WorkflowTabViewModel.Tabs,f.Document)));
            Assert.False(AwaitWorkspace(manager.RunCommunicationOperationAsync(_=>{invoked=true;return Task.CompletedTask;},CancellationToken.None)));
            Assert.False(manager.IsAnyRunning());
        }
        f.Document.ExecutionOwner.DomainRetired+=Reenter;
        try { Assert.True(ServiceInitializer.SolutionManager.Session!.History.Undo());Pump(); }
        finally { f.Document.ExecutionOwner.DomainRetired-=Reenter; }
        Assert.True(attempted);Assert.False(invoked);Assert.Null(ServiceInitializer.SolutionManager.Session!.History.LastObserverError);
        Assert.False(source.Snapshot.ExecutionIdentity!.IsCurrent);Assert.False(downstream.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Null(f.Context.GetNodeResult(f.Source.Id));Assert.Null(f.Context.GetNodeResult(f.Selected.Id));
        Assert.True(AwaitWorkspace(manager.RunCommunicationOperationAsync(_=>Task.CompletedTask,CancellationToken.None)));
        Assert.False(manager.IsAnyRunning());
    });

    private sealed class CanvasGateCommand(Action apply, Action rollback) : SunEyeVision.UI.Features.WorkflowUI.Commands.IUndoableCommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => apply();
        public void Undo() => rollback();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Composite_canvas_edit_excludes_execution_during_apply_commit_and_rollback(bool commit)
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        var manager = Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Main));
        var history = ServiceInitializer.SolutionManager.Session!.History;
        var tab = f.Main.WorkflowTabViewModel.SelectedTab!;
        var before = f.Source.Position;
        var after = new Point(before.X + 5, before.Y + 5);
        var revision = history.Revision;
        var attempts = new List<(string Phase, bool Accepted)>();
        void Attempt(string phase) => attempts.Add((phase,
            AwaitWorkspace(manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, CancellationToken.None))));
        EventHandler changed = (_, _) => Attempt("history");
        history.Changed += changed;
        try
        {
            using (var transaction = tab.CommandManager.BeginTransaction())
            {
                tab.CommandManager.Execute(new CanvasGateCommand(
                    () => { f.Source.Position = after; Attempt("apply"); },
                    () => { Attempt("rollback"); f.Source.Position = before; }));
                Attempt("between");
                if (commit) transaction.Complete();
            }
        }
        finally { history.Changed -= changed; }
        Assert.Null(history.LastObserverError);
        Assert.Contains(attempts, item => item.Phase == "apply");
        Assert.Contains(attempts, item => item.Phase == "between");
        Assert.Contains(attempts, item => item.Phase == (commit ? "history" : "rollback"));
        Assert.All(attempts, item => Assert.False(item.Accepted, item.Phase));
        Assert.Equal(commit ? after : before, f.Source.Position);
        Assert.Equal(revision + (commit ? 1 : 0), history.Revision);
        Assert.True(AwaitWorkspace(manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, CancellationToken.None)));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Canvas_node_collection_observer_failure_restores_the_node_and_history(bool undo)
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        var manager = Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Main));
        var history = ServiceInitializer.SolutionManager.Session!.History;
        var tab = f.Main.WorkflowTabViewModel.SelectedTab!;
        var node = new SunEyeVision.UI.Shared.WorkflowNode("observer-target", "节点", "节点", "ResultWorkspaceProbe")
            { RuntimeConfiguration = RuntimeToolConfiguration.Create("ResultWorkspaceProbe") };
        void AddNode()
        {
            using var transaction = tab.CommandManager.BeginTransaction();
            tab.CommandManager.Execute(new SunEyeVision.UI.Features.WorkflowUI.Commands.AddNodeCommand(f.Flow.Nodes, node));
            transaction.Complete();
        }
        if (undo) AddNode();
        var revision = history.Revision; var position = history.Position; var attempts = 0;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler observer = (_, args) =>
        {
            var relevant = undo ? args.OldItems?.Contains(node) == true : args.NewItems?.Contains(node) == true;
            if (!relevant) return;
            attempts++;
            Assert.False(AwaitWorkspace(manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, CancellationToken.None)));
            throw new InvalidOperationException("受控节点集合通知失败");
        };
        f.Flow.Nodes.CollectionChanged += observer;
        try { Assert.ThrowsAny<Exception>(() => { if (undo) history.Undo(); else AddNode(); }); }
        finally { f.Flow.Nodes.CollectionChanged -= observer; }
        Assert.Equal(1, attempts); Assert.Equal(undo, f.Flow.Nodes.Contains(node));
        Assert.Equal(revision, history.Revision); Assert.Equal(position, history.Position);
        Assert.True(AwaitWorkspace(manager.RunCommunicationOperationAsync(_ => Task.CompletedTask, CancellationToken.None)));
    });
    [Fact]
    public void Active_execution_rejects_composite_canvas_edit_before_its_first_mutation()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(() =>
    {
        using var f = new Fixture();
        var manager = Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Main));
        var history = ServiceInitializer.SolutionManager.Session!.History;
        var tab = f.Main.WorkflowTabViewModel.SelectedTab!;
        var before = f.Source.Position; var revision = history.Revision;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = manager.RunCommunicationOperationAsync(_ => release.Task, CancellationToken.None);
        try
        {
            Assert.True(manager.IsAnyRunning());
            Assert.Throws<InvalidOperationException>(() => tab.CommandManager.BeginTransaction());
            Assert.Equal(before, f.Source.Position); Assert.Equal(revision, history.Revision);
        }
        finally { release.TrySetResult(); Until(() => active.IsCompleted); AwaitWorkspace(active); }
        using (tab.CommandManager.BeginTransaction()) { }
    });
    [Fact]
    public void Active_communication_prevents_history_mutation_without_advancing_or_losing_the_result()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var path=Path.Combine(f.Directory,"active-history.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{9,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);
        var run=f.Publish(f.Source,81);var publication=f.Main.CurrentResultPublication!;
        var history=ServiceInitializer.SolutionManager.Session!.History;var revision=history.Revision;var position=history.Position;
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active=manager.RunCommunicationOperationAsync(_=>release.Task,CancellationToken.None);
        Assert.True(manager.IsAnyRunning());
        try
        {
            Assert.Throws<InvalidOperationException>(()=>history.Undo());
            var version=f.Main.ActiveInputSource!.EditVersion;
            Assert.Throws<InvalidOperationException>(()=>f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}}));
            Assert.Equal(version,f.Main.ActiveInputSource!.EditVersion);Assert.Single(f.Main.ActiveInputSource.Images);
            Assert.Equal(revision,history.Revision);Assert.Equal(position,history.Position);
            Assert.Equal(path,Assert.Single(ImageSourcePolicy.Read(f.Source)!.FilePaths));
            Assert.Same(run.Snapshot,f.Context.GetNodeResult(f.Source.Id)!.Result);
            Assert.True(run.Snapshot.ExecutionIdentity!.IsCurrent);Assert.Same(publication,f.Main.CurrentResultPublication);
        }
        finally { release.TrySetResult();Until(()=>active.IsCompleted);AwaitWorkspace(active); }
        Assert.True(history.Undo());Pump();Assert.False(run.Snapshot.ExecutionIdentity!.IsCurrent);
    });
    [Fact]
    public void Source_acceptance_holds_the_same_execution_gate_through_history_and_visible_list_notifications()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var path=Path.Combine(f.Directory,"source-acceptance-gate.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{10,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        var source=f.Main.ActiveInputSource!;var history=ServiceInitializer.SolutionManager.Session!.History;
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel)
            .GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        var tab=f.Main.WorkflowTabViewModel.SelectedTab!;var attempts=new List<(string Phase,bool Accepted)>();
        void Attempt(string phase)
        {
            attempts.Add((phase+"-continuous",manager.StartContinuousRun(tab)));
            attempts.Add((phase+"-communication",AwaitWorkspace(manager.RunCommunicationOperationAsync(_=>Task.CompletedTask,CancellationToken.None))));
            attempts.Add((phase+"-other-thread",AwaitWorkspace(Task.Run(()=>
            {
                try { using var lease=manager.BeginHistoryMutation();return true; }
                catch(InvalidOperationException) { return false; }
            }))));
        }
        EventHandler historyChanged=(_,_)=>Attempt("history");
        System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChanged=(_,_)=>Attempt("collection");
        EventHandler configurationChanged=(_,_)=>Attempt("configuration");
        history.Changed+=historyChanged;source.CollectionChanged+=collectionChanged;source.ConfigurationEdited+=configurationChanged;
        try { source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true); }
        finally { history.Changed-=historyChanged;source.CollectionChanged-=collectionChanged;source.ConfigurationEdited-=configurationChanged; }
        Assert.Null(history.LastObserverError);Assert.Null(source.LastNotificationError);
        Assert.Equal(9,attempts.Count);Assert.All(attempts,attempt=>Assert.False(attempt.Accepted,attempt.Phase));
        Assert.Equal(path,Assert.Single(source.CapturePolicy().FilePaths));Assert.Equal(0,source.CurrentIndex);
        Assert.False(manager.IsAnyRunning());
        Assert.True(AwaitWorkspace(manager.RunCommunicationOperationAsync(_=>Task.CompletedTask,CancellationToken.None)));
    });
    [Fact]
    public void Source_acceptance_retires_old_queued_runs_without_overwriting_a_reentrant_new_result()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var old=f.Publish(f.Source,90);Results? next=null;
        var path=Path.Combine(f.Directory,"accepted-reentrant.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{11,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        void Reenter(string domain)
        {
            if(next is null&&domain.Contains("/"+f.Flow.Id+"/"+f.Source.Id+"/",StringComparison.Ordinal))next=f.Publish(f.Source,91);
        }
        f.Document.ExecutionOwner.DomainRetired+=Reenter;
        try { f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);Pump(); }
        finally { f.Document.ExecutionOwner.DomainRetired-=Reenter; }
        Assert.NotNull(next);Assert.False(old.Snapshot.ExecutionIdentity!.IsCurrent);Assert.True(next!.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Same(next.Snapshot,f.Context.GetNodeResult(f.Source.Id)!.Result);
        Assert.Equal(next.Snapshot.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);
    });
    [Fact]
    public void Source_acceptance_reports_retirement_observer_failure_after_attempting_all_affected_cleanup()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture(includeOther:true);f.Main.SelectedNode=f.Source;Pump();
        var old=f.Publish(f.Source,92);var downstream=f.Publish(f.Selected,93);var unrelated=f.Publish(f.Other,94);
        old.Snapshot.Dispose();downstream.Snapshot.Dispose();
        var path=Path.Combine(f.Directory,"accepted-retirement-error.png");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{13,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        void Fail(string domain)
        {
            if(domain.Contains("/"+f.Flow.Id+"/"+f.Source.Id+"/",StringComparison.Ordinal))throw new InvalidOperationException("受控退役观察者错误");
        }
        f.Document.ExecutionOwner.DomainRetired+=Fail;
        try { f.Main.ActiveInputSource!.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path,SourceReference=path}},selectFirstAdded:true);Pump(); }
        finally { f.Document.ExecutionOwner.DomainRetired-=Fail; }
        Assert.Equal(path,Assert.Single(ImageSourcePolicy.Read(f.Source)!.FilePaths));
        Assert.Equal(path,Assert.Single(f.Main.ActiveInputSource!.CapturePolicy().FilePaths));
        Assert.False(old.Snapshot.ExecutionIdentity!.IsCurrent);Assert.False(downstream.Snapshot.ExecutionIdentity!.IsCurrent);
        Assert.Null(f.Context.GetNodeResult(f.Source.Id));Assert.Null(f.Context.GetNodeResult(f.Selected.Id));
        Assert.Same(unrelated.Snapshot,f.Context.GetNodeResult(f.Other.Id)!.Result);
        Assert.Equal(1,old.Releases);Assert.Equal(1,downstream.Releases);
        Assert.Contains("图像源配置已接纳，结果失效失败",f.Main.StatusText);
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
    private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T:DependencyObject
    {
        for(var index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
        {
            var child=VisualTreeHelper.GetChild(parent,index);
            if(child is T match)yield return match;
            foreach(var nested in VisualDescendants<T>(child))yield return nested;
        }
    }
    [Fact]
    public void Original_source_mode_popup_opens_and_switches_both_modes_without_running()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var strip=f.View.ImagePreviewContent;
        var mode=Assert.IsType<ComboBox>(strip.FindName("Mode"));
        mode.ApplyTemplate();
        Assert.Equal(SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("运行全部"), Assert.IsType<string>(mode.ToolTip));
        Assert.NotNull(mode.FindResource("RecScrollViewer"));
        Assert.Equal(Color.FromRgb(255,120,0),Assert.IsType<SolidColorBrush>(strip.FindResource("ImageSources.Accent")).Color);
        Assert.Single(Assert.IsAssignableFrom<Panel>(f.Shell.SourceStripContent).Children.Cast<UIElement>());
        var list=Assert.IsType<ListBox>(strip.FindName("lst"));list.ApplyTemplate();
        strip.IsEnabled=false;Pump();
        var listBorder=Assert.IsType<Border>(list.Template.FindName("Bd",list));
        Assert.Equal(Assert.IsType<SolidColorBrush>(strip.FindResource("ImageSources.Background")).Color,Assert.IsType<SolidColorBrush>(listBorder.Background).Color);
        strip.IsEnabled=true;Pump();
        Console.WriteLine("MODE_POPUP before-open");Console.Out.Flush();
        var toggle=Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(mode.Template.FindName("toggle",mode));
        var provider=Assert.IsAssignableFrom<System.Windows.Automation.Provider.IToggleProvider>(new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(toggle).GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle));
        provider.Toggle();f.Host.UpdateLayout();Pump();
        Console.WriteLine("MODE_POPUP opened");Console.Out.Flush();
        var popup=Assert.IsType<System.Windows.Controls.Primitives.Popup>(mode.Template.FindName("Popup",mode));
        Assert.True(popup.IsOpen);Assert.NotNull(popup.Child);Assert.True(popup.Child.IsVisible);
        mode.SelectedIndex=1;Pump();Assert.True(strip.IsSelectMode);Assert.Equal(SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("运行选中"), Assert.IsType<string>(mode.ToolTip));
        mode.IsDropDownOpen=false;Pump();
        provider.Toggle();Pump();Assert.True(popup.IsOpen);mode.SelectedIndex=0;Pump();Assert.False(strip.IsSelectMode);
        mode.IsDropDownOpen=false;Pump();
    });
    [Fact]
    public void Actual_start_page_catalog_projection_skips_unvisited_entries_and_preserves_history_rules()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();
        var settings=new SunEyeVision.UI.Services.Startup.StartPageSettings();
        var preferred=Path.Combine(f.Directory,"preferred.solution");File.WriteAllText(preferred,"history fixture");
        var hidden=Path.Combine(f.Directory,"hidden.solution");var missing=Path.Combine(f.Directory,"moved.solution");
        var time=new DateTime(2026,10,30,12,0,0,DateTimeKind.Local);
        settings.RecentFiles.Add(new(){Name="preferred-history",Path=preferred,LastOpened=new DateTimeOffset(time)});
        settings.DismissedPaths.Add(hidden);
        var metadata=new List<SolutionMetadata>
        {
            new(){Name="never-visited",FilePath=Path.Combine(f.Directory,"never.solution")},
            new(){Name="unsaved",FilePath="",LastAccessTime=time},
            new(){Name="hidden",FilePath=hidden,LastAccessTime=time.AddDays(1)},
            new(){Name="duplicate-catalog",FilePath=preferred,LastAccessTime=time.AddDays(2)},
            new(){Name="moved",FilePath=missing,LastAccessTime=time.AddDays(-1)}
        };
        foreach(var kind in new[]{DateTimeKind.Local,DateTimeKind.Utc,DateTimeKind.Unspecified})
            metadata.Add(new(){Name="valid-"+kind,FilePath=Path.Combine(f.Directory,kind+".solution"),LastAccessTime=DateTime.SpecifyKind(time.AddDays(-2),kind)});
        var build=typeof(SunEyeVision.UI.Services.Startup.StartPageSettings).GetMethod("BuildRecentFiles",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var rows=Assert.IsType<SunEyeVision.UI.Services.Startup.RecentSolutionFile[]>(build.Invoke(settings,new object[]{metadata}));
        Assert.Equal(5,rows.Length);Assert.Equal("preferred-history",rows[0].Name);
        Assert.DoesNotContain(rows,row=>row.Name is "never-visited" or "unsaved" or "hidden" or "duplicate-catalog");
        Assert.Equal(rows.OrderByDescending(row=>row.LastOpened),rows);
        Assert.Equal("文件已移动",rows.Single(row=>row.Name=="moved").DisplayTime);
        foreach(var kind in new[]{DateTimeKind.Local,DateTimeKind.Utc,DateTimeKind.Unspecified})
            Assert.Equal(new DateTimeOffset(DateTime.SpecifyKind(time.AddDays(-2),kind)),rows.Single(row=>row.Name=="valid-"+kind).LastOpened);
        var page=new SunEyeVision.UI.Shell.Windows.StartPageWindow(false,rows,null);
        try{page.Measure(new Size(1020,690));page.Arrange(new Rect(0,0,1020,690));page.UpdateLayout();Assert.NotNull(page.FindName("RecentFilesList"));}
        finally{page.Close();}
        Assert.Equal(default,metadata[0].LastAccessTime);Assert.Single(settings.RecentFiles);Assert.Single(settings.DismissedPaths);
    });
    [Fact]
    public void Original_content_switch_runs_through_formal_acceptance_history_restore_and_host_lifetime()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;
        var path=Path.Combine(f.Directory,"switch.png");var png=new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{12,0,0,0},4)));
        using(var file=File.Create(path))png.Save(file);
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path}},selectFirstAdded:true);Pump();
        source.AutoSwitchEnabled=true;Pump();f.Publish(f.Source,67);Pump();
        var strip=f.View.ImagePreviewContent;
        var original=Assert.IsType<SunEyeVision.Plugin.Wpf.Controls.Inputs.ContentToggleSwitch>(strip.FindName("AutoSwitch"));
        var toggle=Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(original.FindName("TO"));
        var formalWriter=source.ConfigurationWriter!;var writes=0;var starts=0;
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        manager.WorkflowExecutionStarted+=(_,_)=>starts++;
        var history=ServiceInitializer.SolutionManager.Session!.History;var position=history.Position;
        void Click(){typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(toggle,null);Pump();}
        void Snapshot(string state)
        {
            strip.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(strip.ActualWidth),(int)Math.Ceiling(strip.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(strip);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-strip-original-"+state+".png"));encoder.Save(file);
        }
        void Verify(bool value)
        {
            Assert.Equal(value,ImageSourcePolicy.Read(f.Source)!.AutoSwitchEnabled);Assert.Equal(value,source.AutoSwitchEnabled);
            Assert.Equal(value,strip.AutoSwitchEnabled);Assert.Equal(value,original.IsChecked);Assert.Equal(value,toggle.IsChecked);
            Assert.Equal(0,starts);
        }
        Verify(true);Assert.Equal(0,writes);
        source.ConfigurationWriter=_=>{writes++;throw new InvalidOperationException("拒绝开关接纳");};
        Click();Verify(true);Assert.Equal(1,writes);Assert.Equal(position,history.Position);
        Assert.Contains("拒绝开关接纳",strip.Status??string.Empty);
        Assert.NotNull(f.Presenter.OutputPane.CurrentFrame);
        source.ConfigurationWriter=candidate=>{writes++;formalWriter(candidate);};
        var countedWriter=source.ConfigurationWriter;
        Click();Verify(false);Assert.Equal(2,writes);Assert.Equal(position+1,history.Position);Snapshot("off");
        Assert.Null(f.Presenter.OutputPane.CurrentFrame);Assert.Equal(new byte[]{67},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        Click();Verify(true);Assert.Equal(3,writes);Assert.Equal(position+2,history.Position);Snapshot("on");
        var json=System.Text.Json.JsonSerializer.Serialize<WorkflowNodeBase>(f.Source,WorkflowSerializationOptions.Default);
        var restored=System.Text.Json.JsonSerializer.Deserialize<WorkflowNodeBase>(json,WorkflowSerializationOptions.Default)!;
        Assert.True(ImageSourcePolicy.Read(restored)!.AutoSwitchEnabled);
        history.Undo();Pump();Verify(false);history.Redo();Pump();Verify(true);Assert.Equal(3,writes);
        source.IsReadOnly=true;Pump();toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,false);Pump();
        Verify(true);Assert.Equal(3,writes);Assert.Equal(position+2,history.Position);
        source.IsReadOnly=false;Pump();
        f.Host.Content=null;Pump();toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,false);Pump();
        Assert.True(source.AutoSwitchEnabled);Assert.Equal(3,writes);Assert.Equal(0,starts);
        f.Host.Content=f.View;Pump();Assert.Same(original,strip.FindName("AutoSwitch"));Verify(true);
        // Projection may have been reinstalled on reload; count the current formal writer.
        if(!ReferenceEquals(source.ConfigurationWriter,countedWriter))
        {var reloadedWriter=source.ConfigurationWriter!;source.ConfigurationWriter=candidate=>{writes++;reloadedWriter(candidate);};}
        Click();Verify(false);Assert.Equal(4,writes);Assert.Equal(position+3,history.Position);
    });
    [Fact]
    public void Source_direction_keys_keep_focus_after_actual_workflow_execution()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var paths=Enumerable.Range(0,2).Select(index=>Path.Combine(f.Directory,"key-"+index+".png")).ToArray();
        foreach(var path in paths)
        {
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{12,0,0,0},4)));
            using var output=File.Create(path);png.Save(output);
        }
        var source=f.Main.ActiveInputSource!;
        source.AddImages(paths.Select(path=>new SunEyeVision.UI.Shared.ImageInfo{FilePath=path}),selectFirstAdded:true);Pump();
        var strip=f.View.ImagePreviewContent;var list=Assert.IsType<ListBox>(strip.FindName("lst"));
        var shadow=Assert.IsType<Grid>(strip.FindName("RunningShadow"));
        Assert.Equal(Visibility.Collapsed,shadow.Visibility);
        Assert.False(shadow.Focusable);
        var shadowShown=false;shadow.IsVisibleChanged+=(_,e)=>{if((bool)e.NewValue)shadowShown=true;};
        var disabled=false;strip.IsEnabledChanged+=(_,e)=>{if(!(bool)e.NewValue)disabled=true;};
        var gated=false;var rejected=false;
        f.Main.PropertyChanged+=(_,_)=>
        {
            if(gated||f.Main.CanEditRootWorkspace)return;
            gated=true;
            try{source.ClearImages();}catch(InvalidOperationException){rejected=true;}
        };
        f.Host.Activate();f.Host.UpdateLayout();
        var second=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(1));second.ApplyTemplate();
        var row=Assert.Single(VisualDescendants<Grid>(second).Where(grid=>grid.Width==72));
        row.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent});
        Until(()=>!f.Main.IsRunning&&source.CurrentIndex==1);Pump();
        Assert.True(gated,"未经过真实工作流编辑门禁，不能证明生产链。");
        Assert.True(rejected,"运行门禁必须拒绝源配置修改。");Assert.Equal(2,source.Count);
        Assert.False(disabled,"运行不应禁用整条控件并驱逐焦点。");
        Assert.True(shadowShown,"真实运行期间未显示原件独立遮罩。");
        Assert.Equal(Visibility.Collapsed,shadow.Visibility);
        Assert.True(list.IsEnabled,"遮罩不能通过禁用列表驱逐已有键盘焦点。");
        Assert.True(strip.IsKeyboardFocusWithin,"真实工作流结束后焦点离开图像源条。");
        void KeyInput(System.Windows.Input.Key key)
        {
            var target=Assert.IsAssignableFrom<UIElement>(System.Windows.Input.Keyboard.FocusedElement);
            var input=PresentationSource.FromVisual(target)!;
            var preview=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,key){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
            target.RaiseEvent(preview);
            if(!preview.Handled)target.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,key){RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent});
        }
        KeyInput(System.Windows.Input.Key.Left);Until(()=>!f.Main.IsRunning&&source.CurrentIndex==0);Pump();Assert.True(strip.IsKeyboardFocusWithin);
        KeyInput(System.Windows.Input.Key.Right);Until(()=>!f.Main.IsRunning&&source.CurrentIndex==1);Pump();Assert.True(strip.IsKeyboardFocusWithin);
        var rootTab=f.Main.WorkflowTabViewModel.SelectedTab!;
        f.Host.Content=null;Pump();
        Assert.False(System.Windows.Data.BindingOperations.IsDataBound(strip,SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip.IsRunningProperty));
        rootTab.IsRunning=true;Pump();Assert.False(strip.IsRunning);
        rootTab.IsRunning=false;f.Host.Content=f.View;Pump();
        Assert.True(System.Windows.Data.BindingOperations.IsDataBound(strip,SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip.IsRunningProperty));
    });
    [Fact]
    public void Source_configuration_entry_audit_records_run_and_viewport_side_effects()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        var observations=new List<object>();var allImportsSilent=true;
        foreach(var operation in new[]{"files","directory","automatic"})
        {
            using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
            string Write(string name)
            {
                var path=Path.Combine(f.Directory,name);var png=new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{51,0,0,0},4)));
                using var output=File.Create(path);png.Save(output);return path;
            }
            var first=Write("initial.png");var source=f.Main.ActiveInputSource!;
            source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=first}},selectFirstAdded:true);Pump();
            var published=f.Publish(f.Source,31);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
            var before=f.Presenter.OutputPane.CurrentFrame;var starts=0;
            var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
            manager.WorkflowExecutionStarted+=(_,_)=>Interlocked.Increment(ref starts);
            var session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
            var kind=operation=="files"?SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.ImportFiles:
                operation=="directory"?SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.ImportDirectory:
                SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch;
            var added=operation=="automatic"?null:Write("added.png");
            string[]? paths=operation=="files"?new[]{added!}:
                operation=="directory"?new[]{f.Directory}:null;
            var request=new SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequest(kind,session.Version,flag:operation=="automatic"?!source.AutoSwitchEnabled:null,paths:paths);
            AwaitWorkspace(session.RequestAsync(request,CancellationToken.None));Pump();
            var after=f.Presenter.OutputPane.CurrentFrame;
            var preserved=before?.Frame==after?.Frame;
            observations.Add(new{operation,executionStarts=starts,beforeFrame=before?.Frame.ToString(),afterFrame=after?.Frame.ToString(),preserved,source.Count});
            if(operation!="automatic"){Assert.Equal(2,source.Count);allImportsSilent&=starts==0;}
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-configuration-entry-audit.json"),System.Text.Json.JsonSerializer.Serialize(observations,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Assert.True(allImportsSilent,"导入路径触发了正式工作流执行。");
        // AutoPlay rendering is an observation, not a blanket VM conformance assertion; see the original condition matrix.
    });
    [Fact]
    public void Source_fast_direction_keys_preserve_the_latest_choice_after_an_older_run_finishes()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var paths=Enumerable.Range(0,6).Select(index=>Path.Combine(f.Directory,"rapid-"+index+".png")).ToArray();
        for(var index=0;index<paths.Length;index++)
        {
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{(byte)(index+1),0,0,0},4)));
            using var output=File.Create(paths[index]);png.Save(output);
        }
        var source=f.Main.ActiveInputSource!;
        source.AddImages(paths.Select(path=>new SunEyeVision.UI.Shared.ImageInfo{FilePath=path}),selectFirstAdded:true);Pump();
        var strip=f.View.ImagePreviewContent;var list=Assert.IsType<ListBox>(strip.FindName("lst"));
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        var starts=0;var injected=false;var visualAfterKeys=-1;
        var history=ServiceInitializer.SolutionManager.Session!.History;var historyPosition=history.Position;var editVersion=source.EditVersion;
        void KeyInput()
        {
            var target=Assert.IsAssignableFrom<UIElement>(System.Windows.Input.Keyboard.FocusedElement);
            var input=PresentationSource.FromVisual(target)!;
            var preview=new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,System.Windows.Input.Key.Right){RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent};
            target.RaiseEvent(preview);
            if(!preview.Handled)target.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,input,0,System.Windows.Input.Key.Right){RoutedEvent=System.Windows.Input.Keyboard.KeyDownEvent});
        }
        manager.WorkflowExecutionStarted+=(_,_)=>
        {
            starts++;
            if(injected)return;
            injected=true;
            Assert.True(f.Main.IsRunning);
            // The production input snapshot is already frozen, before the source tool executes.
            // Inject real routed keys here so completion cannot race ahead of the test by chance.
            for(var index=0;index<4;index++)KeyInput();
            visualAfterKeys=list.SelectedIndex;
        };
        f.Host.Activate();f.Host.UpdateLayout();
        var second=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(1));second.ApplyTemplate();
        var row=Assert.Single(VisualDescendants<Grid>(second).Where(grid=>grid.Width==72));
        row.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.MouseDownEvent});
        Until(()=>injected&&!f.Main.IsRunning);Pump();
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-fast-keys-audit.json"),
            System.Text.Json.JsonSerializer.Serialize(new{starts,visualAfterKeys,afterCompletion=source.CurrentIndex,listIndex=list.SelectedIndex,source.EditVersion}));
        Assert.True(injected,"未经过正式执行开始边界。");
        Assert.Equal(5,visualAfterKeys);
        Assert.Equal(5,source.CurrentIndex);
        Assert.Equal(5,list.SelectedIndex);
        Assert.Equal(1,starts); // Busy navigation records intent; it does not invent another accepted execution.
        Assert.Equal(historyPosition,history.Position);Assert.Equal(editVersion,source.EditVersion);
        Assert.Equal(0,ImageSourcePolicy.Read(f.Source)!.CurrentIndex);
        Assert.Equal(new byte[]{2},f.Presenter.OutputPane.Viewport.ReadPixel(0,0)); // The old result is not relabelled as the new selection.
        AwaitWorkspace(f.Main.RunSelectedImageAsync());Pump();
        Assert.Equal(2,starts);Assert.Equal(5,source.CurrentIndex);
        Assert.Equal(new byte[]{6},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        Assert.Equal(historyPosition,history.Position);
    });
    [Fact]
    public void Source_sequence_flags_retire_result_authority_but_keep_last_pixels_until_replaced_or_cleared()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var path=Path.Combine(f.Directory,"flag-retention.png");var png=new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{51,0,0,0},4)));
        using(var output=File.Create(path))png.Save(output);
        var source=f.Main.ActiveInputSource!;
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=path}},selectFirstAdded:true);Pump();
        var original=f.Publish(f.Source,31);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        original.Snapshot.Dispose(); // Release the fixture root; production/context/view readers now own its lifetime.
        var publication=f.Main.CurrentResultPublication!;var receipt=f.Presenter.OutputPane.Viewport.CurrentFrame;
        var trace=new List<object>();
        object Observe(string phase,SourceDisplayPreservationScope? scope=null)
        {
            var pane=f.Presenter.OutputPane;
            var active=typeof(ResultImagePane).GetField("_presentation",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane);
            return new{phase,scopeCurrent=scope?.IsCurrent,source.EditVersion,frame=pane.Viewport.CurrentFrame?.ToString(),
                active=active is null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active),
                displayOnly=active?.GetType().GetField("DisplayOnly",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(active)};
        }
        trace.Add(Observe("before"));
        f.Main.SourceDisplayPreservationPreparing+=(_,scope)=>
        {
            trace.Add(Observe("prepared",scope));
            scope.Enlist(affected=>trace.Add(Observe(affected is null?"rejected":"accepted",scope)));
        };
        var lease=f.Presenter.OutputPane.BorrowCurrentImage()!;
        var session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        void Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind kind,bool? flag=null,IEnumerable<string>? ids=null)
            =>AwaitWorkspace(session.RequestAsync(new(kind,session.Version,flag:flag,itemIds:ids),CancellationToken.None));
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,flag:false);Pump();
        trace.Add(Observe("after"));File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!,"source-display-retention-audit.json"),System.Text.Json.JsonSerializer.Serialize(trace,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Assert.False(publication.IsCurrent());Assert.False(lease.IsCurrent);
        Assert.Null(f.Presenter.OutputPane.CurrentFrame);Assert.Null(f.Presenter.OutputPane.BorrowCurrentImage());
        Assert.Equal(receipt,f.Presenter.OutputPane.Viewport.CurrentFrame);
        Assert.Equal(new byte[]{31},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        lease.Dispose();
        var retained=typeof(ResultImagePane).GetField("_presentation",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Presenter.OutputPane)!;
        var retiredLease=Assert.IsAssignableFrom<SunEyeVision.Plugin.Wpf.Presenters.Results.IResultValueLease>(retained.GetType().GetField("Lease",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(retained));
        Assert.Throws<ObjectDisposedException>(()=>{_=retiredLease.Values;});
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetMode,flag:true);
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetRunSelection,ids:new[]{source.Images[0].Id});
        Assert.Equal(receipt,f.Presenter.OutputPane.Viewport.CurrentFrame);
        Assert.Equal(new byte[]{31},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        Assert.Null(f.Presenter.OutputPane.BorrowCurrentImage());
        f.Publish(f.Source,71);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        Assert.Equal(new byte[]{71},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));Assert.Equal(1,original.Releases);
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,flag:true);
        Assert.Null(f.Presenter.OutputPane.CurrentFrame);Assert.Equal(new byte[]{71},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        f.Main.ClearImageCommand.Execute(null);Pump();Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame);
        f.Publish(f.Source,81);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,flag:false);
        File.Delete(path);AwaitWorkspace(f.Main.RunSelectedImageAsync());Pump();
        Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame); // Actual failed run without a new image ends retained display.
        f.Publish(f.Source,91);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,flag:true);
        Assert.Equal(new byte[]{91},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        f.Host.Content=null;Pump();Assert.Null(((ImageViewBox)f.Shell.OutputViewport).ImageDataSource);
        f.Host.Content=f.View;Pump();Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame);
        session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        f.Publish(f.Source,101);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        Request(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,flag:false);
        source.ClearImages();Pump();Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame);
    });
    [Fact]
    public void Source_display_retention_does_not_activate_on_rejection_or_cross_selected_node_reentry()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;
        source.AddImages(new[]{new SunEyeVision.UI.Shared.ImageInfo{FilePath=Path.Combine(f.Directory,"not-read.png")}},selectFirstAdded:true);Pump();
        var original=f.Publish(f.Source,31);Pump();Until(()=>f.Presenter.OutputPane.CurrentFrame is not null);
        var frame=f.Presenter.OutputPane.CurrentFrame!.Frame;
        var session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        var history=ServiceInitializer.SolutionManager.Session!.History;var position=history.Position;var version=source.EditVersion;
        var host=ServiceInitializer.SolutionManager.Session.Editing.Host;
        var failure=typeof(NodeConfigurationEditHost).GetField("_acceptanceFailureAfterRawMutation",BindingFlags.Instance|BindingFlags.NonPublic)!;
        failure.SetValue(host,1);
        try
        {
            Assert.Throws<InvalidOperationException>(()=>AwaitWorkspace(session.RequestAsync(new(
                SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,
                session.Version,flag:false),CancellationToken.None)));
        }
        finally{failure.SetValue(host,-1);}
        Pump();Assert.Equal(position,history.Position);Assert.Equal(version,source.EditVersion);Assert.True(source.AutoSwitchEnabled);
        Assert.Equal(frame,f.Presenter.OutputPane.CurrentFrame!.Frame);
        using(var borrowed=f.Presenter.OutputPane.BorrowCurrentImage()){Assert.NotNull(borrowed);Assert.True(borrowed.IsCurrent);}
        var switched=false;
        Action<SunEyeVision.UI.Shared.ImageInputSource,SourceDisplayPreservationScope> switchSelection=(_,_)=>
        {switched=true;f.Main.SelectedNode=f.Selected;};
        f.Main.SourceDisplayPreservationPreparing+=switchSelection;
        try{AwaitWorkspace(session.RequestAsync(new(
            SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.SetAutoSwitch,
            session.Version,flag:false),CancellationToken.None));}
        finally{f.Main.SourceDisplayPreservationPreparing-=switchSelection;}
        Pump();Assert.True(switched);Assert.Same(f.Selected,f.Main.SelectedNode);Assert.False(source.AutoSwitchEnabled);
        Assert.Null(f.Presenter.OutputPane.CurrentFrame);Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame);
        Assert.Null(f.Presenter.OutputPane.BorrowCurrentImage());
    });
    [Fact]
    public void Original_source_drop_rejects_directories_and_mixed_invalid_files_before_formal_import()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;Assert.Equal(0,source.Count);
        var list=Assert.IsType<ListBox>(f.View.ImagePreviewContent.FindName("lst"));
        var session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        var valid=Path.Combine(f.Directory,"drop.PNG");var invalid=Path.Combine(f.Directory,"drop.txt");
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{91,0,0,0},4)));
        using(var output=File.Create(valid))png.Save(output);File.WriteAllText(invalid,"not an image");
        var history=ServiceInitializer.SolutionManager.Session!.History;var position=history.Position;var version=source.EditVersion;
        var starts=0;var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        manager.WorkflowExecutionStarted+=(_,_)=>starts++;
        var constructor=typeof(DragEventArgs).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)
            .Single(item=>item.GetParameters().Length==5&&item.GetParameters()[0].ParameterType==typeof(IDataObject));
        DragEventArgs Raise(RoutedEvent routed,string[]? paths)
        {
            var data=new DataObject();if(paths is not null)data.SetData(DataFormats.FileDrop,paths);
            var args=(DragEventArgs)constructor.Invoke(new object[]{data,DragDropKeyStates.None,DragDropEffects.Copy,list,new Point(1,1)});
            args.RoutedEvent=routed;list.RaiseEvent(args);return args;
        }
        // Real named-list routed events; this is not a claim of physical desktop drag/drop.
        foreach(var paths in new string[]?[]{null,Array.Empty<string>(),new[]{f.Directory},new[]{invalid},new[]{valid,invalid},new[]{Path.Combine(f.Directory,"missing.png")}})
        {
            Assert.Equal(DragDropEffects.None,Raise(DragDrop.DragEnterEvent,paths).Effects);
            Assert.Equal(DragDropEffects.None,Raise(DragDrop.DropEvent,paths).Effects);
            Assert.Equal(0,source.Count);Assert.Equal(position,history.Position);Assert.Equal(version,source.EditVersion);
        }
        Assert.Equal(DragDropEffects.Copy,Raise(DragDrop.DragEnterEvent,new[]{valid}).Effects);
        Assert.Equal(DragDropEffects.Copy,Raise(DragDrop.DropEvent,new[]{valid}).Effects);
        Until(()=>source.Count==1&&!session.IsImporting);Pump();
        Assert.Equal(valid,Assert.Single(source.Images).FilePath);Assert.Equal(position+1,history.Position);Assert.Equal(0,starts);
        Assert.Equal(new[]{valid},ImageSourcePolicy.Read(f.Source)!.FilePaths);
    });
    [Fact]
    public void Source_structural_edits_keep_the_runtime_record_and_leave_deleted_current_unselected()
        => SunEyeVision.TestShared.WpfTestDispatcher.Invoke(()=>
    {
        using var f=new Fixture();f.Main.SelectedNode=f.Source;Pump();
        var source=f.Main.ActiveInputSource!;
        var paths=Enumerable.Range(0,4).Select(index=>Path.Combine(f.Directory,"structure-"+index+".png")).ToArray();
        for(var index=0;index<paths.Length;index++)
        {
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Gray8,null,new byte[]{(byte)(10+index*10),0,0,0},4)));
            using var output=File.Create(paths[index]);png.Save(output);
        }
        var session=Assert.IsType<ImageSourceStripSession>(f.View.ImagePreviewContent.Session);
        var history=ServiceInitializer.SolutionManager.Session!.History;var opening=history.Position;var starts=0;
        var manager=Assert.IsType<WorkflowExecutionManager>(typeof(MainWindowViewModel).GetField("_executionManager",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(f.Main));
        manager.WorkflowExecutionStarted+=(_,_)=>starts++;
        void Import(params string[] files)=>AwaitWorkspace(session.RequestAsync(new(
            SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.ImportFiles,session.Version,paths:files),CancellationToken.None));
        void Remove(int index)=>AwaitWorkspace(session.RequestAsync(new(
            SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.Remove,session.Version,itemId:source.Images[index].Id),CancellationToken.None));
        void Run(byte expected)
        {
            var before=starts;f.Main.RunWorkflowCommand.Execute(null);Until(()=>starts>before&&!f.Main.IsRunning);Pump();
            Assert.Equal(new[]{expected},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));
        }
        Import(paths.Take(3).ToArray());Assert.Equal(0,starts);Assert.Equal(-1,source.CurrentIndex);
        Assert.Equal(-1,ImageSourcePolicy.Read(f.Source)!.CurrentIndex);Assert.Null(f.Main.NativePreviewPath);
        Run(10);Assert.Equal(0,source.CurrentIndex);
        Import(paths[3]);Assert.Equal(1,starts);Assert.Equal(0,source.CurrentIndex);
        Assert.Equal(new byte[]{10},f.Presenter.OutputPane.Viewport.ReadPixel(0,0));Assert.Null(f.Main.NativePreviewPath);
        Run(20);Assert.Equal(1,source.CurrentIndex);
        Remove(3);Assert.Equal(1,source.CurrentIndex);Run(30);Assert.Equal(2,source.CurrentIndex);
        Remove(0);Assert.Equal(1,source.CurrentIndex);Assert.Equal(paths[2],source.CurrentImage!.FilePath);
        Run(20);Assert.Equal(0,source.CurrentIndex);
        Remove(0);Assert.Equal(-1,source.CurrentIndex);Assert.Null(source.CurrentImage);
        Run(30);Assert.Equal(0,source.CurrentIndex);
        AwaitWorkspace(session.RequestAsync(new(SunEyeVision.Plugin.Wpf.Presenters.Input.ImageSources.ImageSourceStripRequestKind.Clear,session.Version),CancellationToken.None));
        Assert.Equal(0,source.Count);Assert.Equal(-1,source.CurrentIndex);Assert.Null(f.Presenter.OutputPane.Viewport.CurrentFrame);
        Assert.Equal(opening+6,history.Position);Assert.Equal(5,starts);
        history.Undo();Pump();Assert.Single(source.Images);Assert.Equal(-1,source.CurrentIndex);Assert.Equal(5,starts);
        history.Redo();Pump();Assert.Equal(0,source.Count);Assert.Equal(-1,source.CurrentIndex);Assert.Equal(5,starts);
    });
    private static void AwaitWorkspace(Task task)
    {
        Until(()=>task.IsCompleted);
        try{task.GetAwaiter().GetResult();}
        finally{Pump();}
    }
    private static T AwaitWorkspace<T>(Task<T> task)
    {
        AwaitWorkspace((Task)task);return task.GetAwaiter().GetResult();
    }
    private static void Pump()
    { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    private static void Until(Func<bool> ready)
    { var watch = Stopwatch.StartNew(); while (!ready()) { if (watch.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("真实文件preview尚未进入原结果工作区。"); Pump(); Thread.Yield(); } }
}
