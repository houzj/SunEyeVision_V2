using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Windows;
using System.IO;
using System.Runtime.Loader;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class ImageThresholdProductionEditorTests
{
    private static readonly Lazy<Assembly> Plugin = new(() =>
    {
        var hostRoot = typeof(ImageThresholdProductionEditorTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "HostDebugRoot").Value ?? throw new InvalidOperationException("Missing owned host metadata.");
        var path = Path.GetFullPath(Path.Combine(hostRoot, "plugins", "SunEyeVision.Tool.ImageThreshold", "SunEyeVision.Tool.ImageThreshold.dll"));
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        ToolRegistry.RegisterTool(assembly.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool", true)!);
        return assembly;
    });
    private static object? Get(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target);
    private static void Set(object target, string name, object? value) => target.GetType().GetProperty(name)!.SetValue(target, value);
    private static void Method(ToolParameters parameters, string name)
    {
        var property = parameters.GetType().GetProperty("Method")!;
        property.SetValue(parameters, Enum.Parse(property.PropertyType, name));
    }
    private sealed class Fixture : IDisposable
    {
        public readonly WorkflowNodeBase Formal;
        public readonly NodeConfigurationEditHost Host;
        public readonly ToolDebugControlBase Control;
        public readonly Window Window;
        public readonly ProductionEditorContext Context;
        public readonly HashSet<ProductionEditorContext> Active = new();
        public bool Current = true;
        public int Publications;
        public int LegacyCommits;
        public ToolParameters Draft => Context.DraftNode.Parameters;
        public ToolParameters Published => Formal.Parameters;
        public Fixture(bool custom = false)
        {
            var assembly = Plugin.Value;
            var tool = (IToolPlugin)Activator.CreateInstance(assembly.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool", true)!)!;
            var parameters = tool.GetDefaultParameters();
            ((RoiParameters)parameters).InspectionRois.Rois.Add(new Roi { Name = "formal-roi", LocalGeometry = new RoiShape { Geometry = new Polygon2d(new[] { new Point(0, 0), new Point(10, 0), new Point(0, 10) }) } });
            Formal = new WorkflowNodeBase("threshold", "threshold", "threshold", "ImageThreshold") { Parameters = parameters };
            var flow = new SunEyeVision.Workflow.Workflow("flow", "flow"); flow.Nodes.Add(Formal);
            var document = new Solution(); document.Workflows.Add(flow); Host = new(document);
            // Real plugin factory and real standard host, without invoking an algorithm or owning images.
            Control = Assert.IsAssignableFrom<ToolDebugControlBase>(((IToolEditorFactory)tool).CreateDebugControl(tool));
            Window = custom ? new CustomDebugWindow(Control) : new DefaultDebugWindow(Control);
            Window.ShowActivated = false; Window.Left = -10000; Window.Top = -10000;
            var query = new DataSourceQueryService(); query.CommitOutputConfiguration = _ => LegacyCommits++;
            void Bind(WorkflowNodeBase draft)
            { var initializer = (IToolControlInitializer)Window; initializer.SetCurrentNode(draft); initializer.SetParameters(draft.Parameters); initializer.SetDataProvider(query); }
            Context = new ProductionEditorContext(Host, "flow", Formal, () => Current, Bind, _ => Publications++);
            ToolEditorCommit.AttachDraftContext(Control, Context); Bind(Context.DraftNode);
            MainWindowViewModel.TrackProductionEditor(Active, Context, Window, () => { });
            Window.Show(); Window.UpdateLayout();
        }
        private static IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            yield return root;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        public TextBox ThresholdText()
        {
            // Match the real EDIT-SAVE-11 UIA route: select the visible runtime-parameter tab.
            // FindName alone can reach a non-selected tab's detached template, outside commit traversal.
            // ToolDebugControlBase wraps Content in its binding-issue Grid on Loaded.
            var tabs = Visuals(Control).OfType<TabControl>().Single(t => t.Items.OfType<TabItem>().Any(item => Equals(item.Header, "运行参数")));
            tabs.Items.OfType<TabItem>().Single(t => Equals(t.Header, "运行参数")).IsSelected = true;
            Window.UpdateLayout();
            var frame = new System.Windows.Threading.DispatcherFrame();
            Control.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame); Window.UpdateLayout();
            Assert.True(Control.IsLoaded); Assert.NotNull(PresentationSource.FromVisual(Control));
            var config = Assert.IsType<ConfigSetting>(Control.FindName("thresholdParamBinary"));
            Assert.Equal(typeof(int), config.DataType); config.ApplyTemplate();
            var numeric = Assert.IsType<NumericUpDown>(config.Template.FindName("PART_NumericEditor", config)); numeric.ApplyTemplate();
            Assert.True(numeric.IsVisible); Assert.Same(PresentationSource.FromVisual(Control), PresentationSource.FromVisual(numeric));
            DependencyObject? ancestor = numeric;
            while (ancestor is not null && !ReferenceEquals(ancestor, Control)) ancestor = System.Windows.Media.VisualTreeHelper.GetParent(ancestor);
            Assert.Same(Control, ancestor);
            return Assert.IsType<TextBox>(numeric.Template.FindName("PART_TextBox", numeric));
        }
        public Button CustomConfirmButton() => Visuals(Window).OfType<Button>().Single(button =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "ConfirmButton");
        public void Dispose() { Context.Discard(); Window.Close(); }
    }
    [Theory]
    [InlineData(181, false)]
    [InlineData(173, true)]
    public void Real_factory_int32_pending_flows_to_one_publication_before_save_or_confirm(int value, bool confirm) => Sta(() =>
    {
        using var f = new Fixture(); var text = f.ThresholdText(); var version = f.Host.Capture().Version;
        Assert.Null(((RoiParameters)f.Draft).ImageSource); // Saving configuration does not require a runtime image.
        text.Text = value.ToString(); Assert.Equal(128, Get(f.Published, "Threshold"));
        Assert.True(ToolEditorCommit.TryValidateOpenEditors(null, out var error, n => ReferenceEquals(n, f.Formal)), error);
        Assert.Equal(128, Get(f.Published, "Threshold")); Assert.Equal(value, Get(f.Draft, "Threshold"));
        if (confirm) Assert.True(Assert.IsType<DefaultDebugWindow>(f.Window).TryConfirm(out error), error);
        else Assert.True(ToolEditorCommit.TryCommitOpenEditors(null, out error, n => ReferenceEquals(n, f.Formal)), error);
        Assert.Equal(value, Get(f.Published, "Threshold")); Assert.Equal(1, f.Publications);
        Assert.Equal(version + 1, f.Host.Capture().Version); Assert.Equal(0, f.LegacyCommits);
        Assert.NotSame(f.Published, f.Draft);
    });
    [Fact]
    public void Deferred_preview_preference_does_not_block_real_pending_int32_confirmation()
        => Sta(() =>
    {
        using var f = new Fixture();
        var text = f.ThresholdText(); text.Text = "173";
        var preview = new ImageSourceReference(f.Formal.Id, "OutputImage", f.Formal.Name, "输出图像");
        f.Formal.SelectedDisplaySource = preview;
        Assert.True(Assert.IsType<DefaultDebugWindow>(f.Window).TryConfirm(out var error), error);
        Assert.Equal(173, Get(f.Published, "Threshold")); Assert.Equal(1, f.Publications);
        Assert.Same(preview, f.Formal.SelectedDisplaySource); Assert.False(f.Window.IsVisible);
        f.Host.Undo(); Assert.Equal(128, Get(f.Published, "Threshold")); Assert.Same(preview, f.Formal.SelectedDisplaySource);
        f.Host.Redo(); Assert.Equal(173, Get(f.Published, "Threshold")); Assert.Same(preview, f.Formal.SelectedDisplaySource);
    });

    [Fact]
    public void Custom_host_visible_confirm_rejects_invalid_then_invokes_exactly_one_publication_and_closes() => Sta(() =>
    {
        using var f = new Fixture(custom: true);
        var text = f.ThresholdText(); var version = f.Host.Capture().Version;
        var window = Assert.IsType<CustomDebugWindow>(f.Window);
        var button = f.CustomConfirmButton();
        Assert.True(button.IsVisible); Assert.True(button.IsEnabled);
        Assert.Same(PresentationSource.FromVisual(f.Control), PresentationSource.FromVisual(button));
        text.Text = "not-int";
        Assert.False(window.TryConfirm(out var error)); Assert.NotEmpty(error);
        Assert.True(window.IsVisible); Assert.Equal("not-int", text.Text);
        Assert.Equal(version, f.Host.Capture().Version); Assert.Equal(0, f.Publications);
        Assert.Equal(128, Get(f.Published, "Threshold"));
        text.Text = "173";
        var peer = new System.Windows.Automation.Peers.ButtonAutomationPeer(button);
        var invoke = Assert.IsAssignableFrom<System.Windows.Automation.Provider.IInvokeProvider>(
            peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke));
        invoke.Invoke();
        var frame = new System.Windows.Threading.DispatcherFrame();
        button.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        Assert.False(window.IsVisible); Assert.Equal(173, Get(f.Published, "Threshold"));
        Assert.Equal(1, f.Publications); Assert.Equal(0, f.LegacyCommits);
        Assert.False(f.Context.IsActive); Assert.Empty(f.Active);
    });
    [Fact]
    public void Invalid_pending_keeps_real_text_and_formal_config_confirm_fails_and_close_discards() => Sta(() =>
    {
        using var f = new Fixture(); var text = f.ThresholdText(); var version = f.Host.Capture().Version;
        text.Text = "not-int";
        Assert.False(ToolEditorCommit.TryValidateOpenEditors(null, out _, n => ReferenceEquals(n, f.Formal)));
        Assert.False(Assert.IsType<DefaultDebugWindow>(f.Window).TryConfirm(out _)); Assert.Equal("not-int", text.Text);
        Assert.Equal(128, Get(f.Published, "Threshold")); Assert.Equal(version, f.Host.Capture().Version);
        Assert.Equal(0, f.Publications); Assert.Equal(0, f.LegacyCommits);
        Assert.True(ToolEditorClose.TryClose(f.Control, f.Window, _ => throw new Exception("Draft close must not request legacy commit")));
        f.Window.Close(); Assert.False(f.Context.IsActive); Assert.Empty(f.Active); Assert.Equal(0, f.Publications);
    });
    [Theory]
    [InlineData("enum")]
    [InlineData("odd-block")]
    [InlineData("c-range")]
    [InlineData("threshold-order")]
    [InlineData("result-range")]
    [InlineData("result-null")]
    [InlineData("result-key")]
    public void Full_configuration_validation_blocks_invalid_draft_without_publishing(string invalid) => Sta(() =>
    {
        using var f = new Fixture(); var version = f.Host.Capture().Version;
        switch (invalid)
        {
            case "enum":
                var property = f.Draft.GetType().GetProperty("Method")!;
                property.SetValue(f.Draft, Enum.ToObject(property.PropertyType, 999)); break;
            case "odd-block": Method(f.Draft, "AdaptiveMean"); Set(f.Draft, "BlockSize", 4); break;
            case "c-range": Method(f.Draft, "AdaptiveMean"); Set(f.Draft, "C", -1d); break;
            case "threshold-order": Method(f.Draft, "InRange"); Set(f.Draft, "MinThreshold", 200); Set(f.Draft, "MaxThreshold", 100); break;
            case "result-null": Set(f.Draft, "ResultPresentation", null); break;
            case "result-key": ((ResultPresentationConfig)Get(f.Draft, "ResultPresentation")!).Judgments[1].Key = "output-not-empty"; break;
            default:
                var judgment = ((ResultPresentationConfig)Get(f.Draft, "ResultPresentation")!).Judgments[1];
                judgment.Enabled = true; judgment.Min = 20; judgment.Max = 10; break;
        }
        Assert.False(f.Context.TryValidateEditor(out var error)); Assert.NotEmpty(error);
        Assert.False(f.Context.TryCommitEditor(out _)); Assert.Equal(version, f.Host.Capture().Version);
        Assert.Equal(128, Get(f.Published, "Threshold")); Assert.Equal(0, f.Publications);
    });
    [Fact]
    public void Roi_and_result_configuration_are_deep_drafts_before_publish_after_rebind_and_on_cancel() => Sta(() =>
    {
        using var f = new Fixture(); var formal = (RoiParameters)f.Published; var draft = (RoiParameters)f.Draft;
        var results = (ResultPresentationConfig)Get(f.Draft, "ResultPresentation")!;
        var publishedResults = (ResultPresentationConfig)Get(f.Published, "ResultPresentation")!;
        Assert.NotSame(formal.InspectionRois, draft.InspectionRois);
        Assert.NotSame(formal.InspectionRois.Rois[0], draft.InspectionRois.Rois[0]);
        Assert.NotSame(formal.InspectionRois.Rois[0].LocalGeometry, draft.InspectionRois.Rois[0].LocalGeometry);
        var formalPolygon = Assert.IsType<Polygon2d>(formal.InspectionRois.Rois[0].LocalGeometry!.Geometry);
        var draftPolygon = Assert.IsType<Polygon2d>(draft.InspectionRois.Rois[0].LocalGeometry!.Geometry);
        Assert.NotSame(formalPolygon.Vertices, draftPolygon.Vertices); draftPolygon.Vertices[0] = new Point(5, 6);
        Assert.Equal(new Point(0, 0), formalPolygon.Vertices[0]);
        Assert.NotSame(results, publishedResults); Assert.NotSame(results.Judgments[1], publishedResults.Judgments[1]);
        draft.InspectionRois.Rois[0].Name = "draft-roi"; draft.InspectionRois.Rois.Add(new Roi { Name = "new-roi" });
        results.Judgments[1].Min = 2;
        Assert.Equal("formal-roi", formal.InspectionRois.Rois[0].Name); Assert.Single(formal.InspectionRois.Rois);
        Assert.Equal(0, publishedResults.Judgments[1].Min);
        Assert.True(f.Context.TryCommitEditor(out var error), error); Assert.Equal(1, f.Publications);
        Assert.Equal("draft-roi", ((RoiParameters)f.Published).InspectionRois.Rois[0].Name);
        Assert.Equal(new Point(5, 6), Assert.IsType<Polygon2d>(((RoiParameters)f.Published).InspectionRois.Rois[0].LocalGeometry!.Geometry).Vertices[0]);
        Assert.IsType<Polygon2d>(((RoiParameters)f.Draft).InspectionRois.Rois[0].LocalGeometry!.Geometry).Vertices[0] = new Point(9, 9);
        ((RoiParameters)f.Draft).InspectionRois.Rois[0].Name = "discarded";
        ((ResultPresentationConfig)Get(f.Draft, "ResultPresentation")!).Judgments[1].Min = 3;
        f.Context.Discard(); Assert.Empty(f.Active);
        Assert.Equal("draft-roi", ((RoiParameters)f.Published).InspectionRois.Rois[0].Name);
        Assert.Equal(2, ((ResultPresentationConfig)Get(f.Published, "ResultPresentation")!).Judgments[1].Min);
        Assert.Equal(new Point(5, 6), Assert.IsType<Polygon2d>(((RoiParameters)f.Published).InspectionRois.Rois[0].LocalGeometry!.Geometry).Vertices[0]);
        Assert.False(f.Context.TryCommitEditor(out _)); Assert.Equal(1, f.Publications);
    });
    [Fact]
    public void Stale_session_cannot_publish_even_when_another_document_has_same_local_node_id() => Sta(() =>
    {
        using var f = new Fixture(); var other = new WorkflowNodeBase("threshold", "other", "other", "ImageThreshold") { Parameters = Plugin.Value.CreateInstance("SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters") as ToolParameters ?? throw new Exception() };
        Assert.Equal(f.Formal.Id, other.Id); Assert.NotSame(f.Formal, other);
        var text = f.ThresholdText(); text.Text = "181"; f.Current = false;
        Assert.False(ToolEditorCommit.TryCommitOpenEditors(null, out _, n => ReferenceEquals(n, f.Formal)));
        Assert.False(f.Context.IsActive); Assert.Empty(f.Active);
        Assert.Equal(128, Get(f.Published, "Threshold")); Assert.Equal(0, f.Publications);
    });
    [Fact]
    public void Custom_host_forwards_real_editor_and_closing_does_not_repeat_publication() => Sta(() =>
    {
        using var f = new Fixture(custom: true);
        Assert.Same(f.Control, Assert.IsType<CustomDebugWindow>(f.Window).EditorControl);
        f.ThresholdText().Text = "181";
        Assert.True(ToolEditorCommit.TryCommitOpenEditors(null, out var error, n => ReferenceEquals(n, f.Formal)), error);
        Assert.Equal(181, Get(f.Published, "Threshold")); Assert.Equal(1, f.Publications);
        f.Window.Close(); Assert.Equal(1, f.Publications); Assert.Empty(f.Active); Assert.Equal(0, f.LegacyCommits);
    });
    // A real application owns its window chrome and all editor windows on one UI dispatcher.
    // Keep this fixture on that same threading model rather than creating a dispatcher per case.
    private static readonly Lazy<System.Windows.Threading.Dispatcher> UiThread = new(() =>
    {
        System.Windows.Threading.Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set(); System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true, Name = "ImageThreshold editor fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(10))); return dispatcher!;
    });
    internal static void Sta(Action action) => UiThread.Value.Invoke(action);
}
