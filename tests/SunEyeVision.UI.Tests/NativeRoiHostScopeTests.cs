using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageView;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Editing.Regions;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class NativeRoiHostScopeTests
{
    private sealed class Frame : IDisposable
    {
        public readonly IReadOnlyImage Image;
        public readonly ResultPublication Publication;
        public readonly ResultImagePane Pane;
        public readonly ImageViewBox View = new();
        public Frame()
        {
            var resources = new FrozenOutputResources();
            var frame = new ImageFrameAddress(Guid.NewGuid(), "actual-node-input");
            var coordinates = new CoordinateFrame("actual-domain", CoordinateDomain.Image, CoordinateUnit.Pixel);
            var domain = resources.CreateRegion(coordinates, frame, Enumerable.Range(0, 20).Select(y => new RegionRun(y, 0, 29)).ToArray());
            Image = resources.CreateImage(new ImageLayout(30, 20, 30, PixelLayout.Gray8), frame, domain, new byte[600]);
            var type = TypeRef.ResourceOf(ResourceKind.Image); var schema = new ToolSchema(Array.Empty<Port>(), new[] { new Port("image", type) });
            var run = new RunIdentity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "input", Guid.NewGuid());
            Publication = new(schema, new(Guid.NewGuid(), run, 1, schema.Fingerprint), ResultViewState.Success,
                new InputValues(schema.Outputs, new Dictionary<string, object?> { ["image"] = Image }), resources, () => true);
            Pane = new(View);
            Assert.True(Pane.TryPresent(Publication, new("input", "image", type, scopeId: "flow"), "flow", () => true, out var error), error);
        }
        public void Dispose() { Pane.Dispose(); Publication.Dispose(); }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly Solution Document;
        public readonly WorkflowNodeBase Formal;
        public readonly NodeConfigurationEditHost Host;
        public readonly ProductionEditorContext Context;
        public readonly RoiViewportEditScope Scope;
        public Frame Input = new();
        public bool Current = true;
        public bool RebindAfterInstall;
        public int Publishes;
        public RoiParameters Draft => Assert.IsAssignableFrom<RoiParameters>(Context.DraftNode.Parameters);
        public Fixture()
        {
            var root = typeof(NativeRoiHostScopeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "HostDebugRoot").Value!;
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "plugins", "SunEyeVision.Tool.ImageThreshold", "SunEyeVision.Tool.ImageThreshold.dll"));
            var toolType = assembly.GetType("SunEyeVision.Tool.ImageThreshold.ImageThresholdTool", true)!; ToolRegistry.RegisterTool(toolType);
            var tool = (IToolPlugin)Activator.CreateInstance(toolType)!;
            Formal = new("roi", "roi", "roi", "ImageThreshold") { Parameters = tool.GetDefaultParameters() };
            var original = (RoiParameters)Formal.Parameters;
            original.InspectionRois.Rois.Add(Roi.CreateLocal("retained-circle", new Circle2d(5, 5, 2)));
            original.InspectionRois.Rois.Add(Roi.CreateLocal("retained-partial-annulus", new Annulus2d(new Point(8, 8), 1, 3, 0, Math.PI)));
            var flow = new SunEyeVision.Workflow.Workflow("flow", "flow"); flow.Nodes.Add(Formal);
            Document = new Solution(); Document.Workflows.Add(flow); Host = new(Document);
            Context = new(Host, "flow", Formal, () => Current, _ => { });
            Scope = new(() => Input.Pane.BorrowCurrentImage(), () => Current && Context.IsValid,
                () => RoiParameterEditingAdapter.Read(Draft, new[] { "inspection", "mask" }), proposal =>
                {
                    var copy = (RoiParameters)ToolExecutionPreparation.CopyParameters(Draft);
                    var selection = RoiParameterEditingAdapter.Apply(copy, proposal);
                    var previous = Context.DraftNode.Parameters; Context.DraftNode.Parameters = copy;
                    try { Host.Validate(Context.PreparedDraft); }
                    catch { Context.DraftNode.Parameters = previous; throw; }
                    var rows = RoiParameterEditingAdapter.Read(copy, new[] { "inspection", "mask" });
                    if (RebindAfterInstall) Current = false;
                    return (selection, rows);
                }, () => Publishes++);
            Context.PrepareView = () => Scope.HasPendingGesture ? "原区域手势尚未完成。" : null;
            Scope.SetEditableGroups(new[] { "inspection", "mask" }); Scope.SetScene(RoiParameterEditingAdapter.Read(Draft, new[] { "inspection", "mask" }));
            Scope.Activate(); Assert.NotNull(Session);
        }
        public NativeRoiSession Session => Assert.IsType<NativeRoiSession>(typeof(RoiViewportEditScope).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Scope));
        public bool Draw(string kind)
        {
            Assert.True(Scope.BeginDraw("inspection", kind, out var error), error);
            var request = Assert.IsType<RoiDrawRequest>(Session.ActiveDraw);
            var shape = RoiGeometryEditing.Create(kind, new(10, 10), new(16, 14),
                kind == "polygon" ? new[] { new Point2(2, 2), new Point2(8, 2), new Point2(5, 8) } : null);
            return Session.CompleteDraw(request, shape);
        }
        public void Dispose() { Scope.Dispose(); Context.Dispose(); Input.Dispose(); }
    }
    [Fact]
    public void Native_create_replace_delete_keep_unsupported_private_shapes_and_apply_history_saved_identity()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); var retained = f.Draft.InspectionRois.Rois.Select(row => row.Id).ToArray();
        Assert.Empty(f.Session.Items); Assert.False(f.Session.History.CanUndo); Assert.False(f.Session.TryUndo());
        Assert.False(f.Scope.BeginDraw("inspection", "arc", out var diagnostic)); Assert.NotEmpty(diagnostic);
        Assert.True(f.Draw("rectangle")); Assert.Equal(3, f.Draft.InspectionRois.Rois.Count);
        Assert.Equal(2, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count); Assert.False(f.Host.CanUndo);
        var rectangle = Assert.IsType<NativeBoxRoiProjection>(Assert.Single(f.Session.Items)); var id = rectangle.Id;
        rectangle.Width = .4; Assert.True(f.Scope.HasPendingGesture);
        Assert.False(f.Context.TryCommitEditor(out var pending)); Assert.NotEmpty(pending);
        Assert.True(f.Session.TryCommit(rectangle)); Assert.False(f.Scope.HasPendingGesture);
        Assert.Equal(12, f.Draft.InspectionRois.Rois.Single(row => row.Id == id).LocalGeometry!.Width);
        Assert.True(f.Context.TryCommitEditor(out var error), error); Assert.True(f.Host.CanUndo);
        Assert.Equal(3, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count);
        f.Host.Undo(); Assert.Equal(2, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count);
        f.Host.Redo(); Assert.Equal(id, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Last().Id);
        var directory = Environment.GetEnvironmentVariable("S2_CASE_ROOT")!;
        var file = Path.Combine(directory, "native-roi-" + Guid.NewGuid().ToString("N") + ".solution");
        Assert.True(new SolutionRepository().Save(f.Document, file));
        var saved = (RoiParameters)new SolutionRepository().Load(file)!.Workflows.Single().Nodes.Single().Parameters;
        Assert.Equal(retained, saved.InspectionRois.Rois.Take(2).Select(row => row.Id));
        Assert.IsType<Circle2d>(saved.InspectionRois.Rois[0].LocalGeometry!.Geometry);
        Assert.Equal(Math.PI, Assert.IsType<Annulus2d>(saved.InspectionRois.Rois[1].LocalGeometry!.Geometry).EndAngle);
        // Rebind the now accepted draft to the same actual input; delete goes directly through native proposals.
        f.Scope.SetScene(RoiParameterEditingAdapter.Read(f.Draft, new[] { "inspection", "mask" }));
        Assert.True(f.Session.TryDelete(f.Session.Items.ToArray()));
        Assert.Equal(retained, f.Draft.InspectionRois.Rois.Select(row => row.Id));
        Assert.Equal(3, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count);
    });
    [Fact]
    public void Complete_native_shape_set_and_both_roles_share_one_private_candidate_without_sector_coercion()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture();
        foreach (var kind in new[] { "rectangle", "annulus", "polygon" }) Assert.True(f.Draw(kind));
        Assert.False(f.Scope.BeginDraw("inspection", "sector", out var error)); Assert.NotEmpty(error);
        Assert.Equal(5, f.Draft.InspectionRois.Rois.Count); Assert.Equal(3, f.Session.Items.Count);
        Assert.True(f.Scope.BeginDraw("mask", "rectangle", out error), error);
        var request = f.Session.ActiveDraw!;
        Assert.True(f.Session.CompleteDraw(request, RoiGeometryEditing.Create("rectangle", new(3, 3), new(9, 9))));
        Assert.Equal("mask", f.Session.CurROIData!.Group);
        var maskId = Assert.Single(f.Draft.MaskRois.Rois).Id;
        Assert.True(f.Context.TryCommitEditor(out error), error);
        var formal = (RoiParameters)f.Formal.Parameters;
        Assert.Equal(5, formal.InspectionRois.Rois.Count); Assert.Equal(maskId, Assert.Single(formal.MaskRois.Rois).Id);
        Assert.Equal(Math.PI, Assert.IsType<Annulus2d>(formal.InspectionRois.Rois[1].LocalGeometry!.Geometry).EndAngle);
        Assert.Equal(2 * Math.PI, Assert.IsType<Annulus2d>(formal.InspectionRois.Rois[3].LocalGeometry!.Geometry).EndAngle);
        f.Host.Undo(); Assert.Empty(((RoiParameters)f.Formal.Parameters).MaskRois.Rois);
        f.Host.Redo(); Assert.Equal(maskId, Assert.Single(((RoiParameters)f.Formal.Parameters).MaskRois.Rois).Id);
    });
    [Fact]
    public void Real_pending_draw_cancel_and_scope_disposal_leave_original_image_and_formal_configuration()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); Assert.True(f.Scope.BeginDraw("inspection", "polygon", out _));
        Assert.True(f.Scope.HasPendingGesture); Assert.False(f.Context.TryCommitEditor(out _));
        f.Scope.CancelGesture(); Assert.False(f.Scope.HasPendingGesture); Assert.Null(f.Session.ActiveDraw);
        f.Scope.Dispose(); Assert.NotNull(f.Input.Pane.CurrentFrame); Assert.NotNull(f.Input.View.ImageDataSource);
        Assert.Equal(2, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count); Assert.False(f.Host.CanUndo);
    });
    [Fact]
    public void New_input_epoch_retires_only_old_edit_scope_and_rejects_old_request()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); Assert.True(f.Scope.BeginDraw("inspection", "rectangle", out _));
        var oldSession = f.Session; var request = oldSession.ActiveDraw!; var old = f.Input;
        f.Input = new Frame(); f.Scope.Activate();
        try
        {
            Assert.NotSame(oldSession, f.Session); Assert.Equal(f.Input.Image.Frame, f.Session.Frame);
            Assert.Equal(f.Input.Image.EffectiveDomain.Frame, f.Session.Coordinates);
            Assert.False(oldSession.CompleteDraw(request, RoiGeometryEditing.Create("rectangle", new(1, 1), new(3, 3))));
            Assert.NotNull(old.Pane.CurrentFrame); Assert.NotNull(old.View.ImageDataSource);
            Assert.Equal(2, f.Draft.InspectionRois.Rois.Count);
        }
        finally { old.Dispose(); }
    });
    [Fact]
    public void Installed_private_candidate_survives_view_retirement_without_old_publish_or_live_apply()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        using var f = new Fixture(); f.RebindAfterInstall = true;
        Assert.True(f.Draw("rectangle")); Assert.Equal(0, f.Publishes);
        Assert.Equal(3, f.Draft.InspectionRois.Rois.Count);
        Assert.Equal(2, ((RoiParameters)f.Formal.Parameters).InspectionRois.Rois.Count);
        Assert.False(f.Context.TryCommitEditor(out _)); Assert.False(f.Scope.IsActive);
    });
}
