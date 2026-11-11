using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Editing;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class BlobThreePageSessionRegressionTests
{
    private static KeyEventArgs RaiseKey(FrameworkElement element, Key key, RoutedEvent routedEvent)
    {
        var source = Assert.IsAssignableFrom<PresentationSource>(PresentationSource.FromVisual(element));
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = routedEvent };
        element.RaiseEvent(args);
        return args;
    }
    [Fact]
    public void A_failed_lazy_page_retires_the_current_blob_composition_and_blocks_later_slots()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var fixture = new DocumentUiFixture();
            var blob = fixture.Add("BlobAnalysis");
            var draft = fixture.Session.Editing.Host.BeginEdit(fixture.Root.Id, blob.Id);
            var ports = draft.SettingsSchema.Inputs.Concat(draft.DisplaySchema.Inputs);
            var visited = new List<string>();
            using var view = DesktopToolPresentation.CreateSettingsView(
                fixture.Session.AdmitPresentation(fixture.Root.Id, blob.Id), draft.Configuration.Fields.Select(ports),
                new ToolSettingsViewOptions(PageBuilt: (page, _, _) =>
                {
                    visited.Add(page);
                    if (page == "运行参数") throw new InvalidOperationException("测试页组装失败");
                }));
            var editor = Assert.IsType<DeclaredSettingsView>(view.View.FindName("Editor"));
            Assert.Equal(0, editor.SelectedPageIndex);
            editor.SelectedPageIndex = 1;
            Assert.True(view.HasInvalidInput);
            Assert.False(view.TryCapture(out _, out var error));
            Assert.Equal("测试页组装失败", error);
            editor.SelectedPageIndex = 2;
            Assert.Equal(new[] { "基本参数", "运行参数" }, visited);
        });
    }

    [Fact]
    public void Changing_roi_area_retires_the_visible_source_and_commits_one_undoable_edit()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            const string mode = "input.roi.inheritance-mode";
            const string rectangle = "input.roi.rectangle";
            using var fixture = new DocumentUiFixture();
            var image = fixture.Add();
            var producer = fixture.Add("BlobAnalysis", 230, 160);
            var consumer = fixture.Add("BlobAnalysis", 350, 260);
            fixture.ConfigureImage(image, fixture.Image(180, 8));
            fixture.Connect(image, producer);
            fixture.Connect(producer, consumer);
            var imagePort = fixture.Session.GetNodeSchema(fixture.Root.Id, image.Id)!.Outputs.Single(item => item.Id == "output.image");
            var port = fixture.Session.GetNodeSchema(fixture.Root.Id, producer.Id)!.Outputs
                .Single(item => item.Id == "field:8ca10000000040008000000000000114");
            var source = new ReferenceSetting(new OutputReference(producer.Id, port.Id, port.Type, scopeId: fixture.Root.Id));
            fixture.Fields(consumer, ("input.image", new ReferenceSetting(
                new OutputReference(image.Id, imagePort.Id, imagePort.Type, new[] { "data" }, scopeId: fixture.Root.Id))),
                (mode, new ConstantSetting("Rectangle")), (rectangle, source),
                (rectangle + ".center-x", FromRectangle("width")),
                (rectangle + ".center-y", FromRectangle("center", "y")),
                (rectangle + ".width", FromRectangle("width")),
                (rectangle + ".height", FromRectangle("height")),
                (rectangle + ".angle-degrees", FromRectangle("angle-degrees")));
            ReferenceSetting FromRectangle(params string[] path) =>
                new(new OutputReference(producer.Id, port.Id, port.Type, path, scopeId: fixture.Root.Id));
            fixture.Run();
            using (var result = fixture.Session.AcquirePublishedResult(fixture.Root.Id, consumer.Id))
            {
                Assert.True(result?.Snapshot.IsSuccess, result?.Snapshot.ErrorMessage);
                var geometry = Assert.IsAssignableFrom<IToolResultGeometryOutput>(result!.Snapshot.Output);
                var detection = Assert.Single(geometry.ResultGeometry.Groups.Single(group =>
                    group.Role == ResultGeometryRole.Inspection).Shapes);
                Assert.Equal("rectangle", detection.Get<string>("kind"));
            }
            fixture.Main.SelectedNode = consumer;
            var shell = Assert.Single(UiRuntime.Visuals<ResultWorkspaceShell>(fixture.Results));
            shell.SetCurrentValue(ResultWorkspaceShell.IsDoubleImageProperty, true);
            fixture.Main.OpenDebugWindowCommand.Execute(consumer);
            UiRuntime.Pump();
            var window = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
            try
            {
                UiRuntime.AwaitEditorReady(window);
                window.UpdateLayout();
                UiRuntime.Pump();
                var roi = Assert.Single(UiRuntime.Visuals<NativeRoiUiDraftControls>(window.Editor));
                var rows = roi.Children.OfType<DeclaredFieldRow>().ToArray();
                DeclaredFieldRow Choice(string caption) => rows.Single(row => Assert.IsType<FieldRowModel>(row.Content).DisplayNameA == caption);
                var creation = Choice("ROI Creation");
                var inheritance = Choice("Inheritance Mode");
                var region = rows.Single(row => row.FieldId == rectangle);
                var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
                DeclaredFieldRow[] AssertSourceRows(string prefix, params string[] labels)
                {
                    window.UpdateLayout();
                    UiRuntime.Pump();
                    var fields = UiRuntime.Visuals<DeclaredFieldRow>(roi)
                        .Where(row => row.IsVisible && row.FieldId.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                    Assert.Equal(labels, fields.Select(row => Assert.IsType<FieldRowModel>(row.Content).DisplayNameA).ToArray());
                    Assert.All(fields, row => Assert.Equal(DeclaredFieldTemplateRegistry.Reference, Assert.IsType<FieldRowModel>(row.Content).TemplateKey));
                    return fields;
                }
                Assert.IsType<ReferenceSetting>(region.Setting);
                Assert.True(inheritance.TrySetConstant(1L));
                var parameters = AssertSourceRows(rectangle + ".", "roicenterx", "roicentery", "roiwidth", "roiheight", "roiangle");
                var inputViewport = Assert.IsType<ImageViewBox>(shell.InputViewport);
                IRectangle Preview() => Assert.Single(inputViewport.ShapeList.OfType<Tuple<string, object>>()
                    .SelectMany(group => ((IEnumerable<object>)group.Item2).OfType<IRectangle>()),
                    shape => shape.Stroke == Color.FromRgb(15,106,243).ToString());
                var oldCenter = Preview().CenterPoint;
                var centerX = parameters[0];
                var editor = Assert.IsType<FieldRowModel>(centerX.Content).ReferenceDataA!;
                object? Read(object owner, string property) => owner.GetType().GetProperty(property)!.GetValue(owner);
                void Execute(string property, object? argument = null) =>
                    Assert.IsAssignableFrom<System.Windows.Input.ICommand>(Read(editor, property)).Execute(argument);
                IEnumerable<object> Nodes(object owner, string property) =>
                    Assert.IsAssignableFrom<System.Collections.IEnumerable>(Read(owner, property)).Cast<object>();
                IEnumerable<object> Descendants(object node) => new[] { node }.Concat(Nodes(node, "EnumEntrysA").SelectMany(Descendants));
                Execute("DropDownCommand");
                var candidate = Nodes(editor, "SourceNodes").SelectMany(Descendants)
                    .Single(node => Read(node, "Candidate") is DeclaredReferenceCandidate item
                        && item.NodeId == producer.Id && item.Output.Id == port.Id && item.MemberPath.SequenceEqual(new[] { "center", "x" }));
                Execute("ClickCommand", candidate);
                Assert.True(roi.TryCaptureActiveSources(out var sourceError),sourceError);
                var linked = Assert.IsType<ReferenceSetting>(context.PreparedDraft.Configuration.Fields.Read(centerX.FieldId));
                Assert.Equal(new[] { "center", "x" }, linked.Reference.MemberFieldPath);
                UiRuntime.Pump();
                Assert.NotEqual(oldCenter, Preview().CenterPoint);
                Assert.IsType<ReferenceSetting>(region.Setting);
                var area = Choice("ROI Area Type");
                Assert.Equal("input.roi.area-type",area.FieldId);
                Assert.Equal("RadioSelecter",Assert.IsType<FieldRowModel>(area.Content).TemplateKey);
                Assert.True(area.TrySetConstant(1L));
                Assert.Equal(1L,context.PreparedDraft.Configuration.Fields.Get<long>(area.FieldId));
                Assert.False(inheritance.IsVisible);
                Assert.IsType<UnsetSetting>(region.Setting);
                Assert.IsType<UnsetSetting>(centerX.Setting);
                Assert.Equal("RectangleParameters", context.PreparedDraft.Configuration.Fields.Get<string>(mode));
                Assert.True(area.TrySetConstant(0L));
                Assert.True(inheritance.IsVisible);
                Assert.True(centerX.IsVisible);
                Assert.IsType<UnsetSetting>(centerX.Setting);
                Assert.True(inheritance.TrySetConstant(2L));
                Assert.IsType<UnsetSetting>(centerX.Setting);
                Assert.True(inheritance.TrySetConstant(3L));
                AssertSourceRows("input.roi.annulus.", "roiAnnulusCenterX", "roiAnnulusCenterY", "CircleInnerRadius", "roiAnnulusOuterRadius", "Angle Start", "AngleRange");
                Assert.True(inheritance.TrySetConstant(0L));
                Assert.Equal(Visibility.Visible, region.Visibility);
                Assert.IsType<UnsetSetting>(region.Setting);
                Assert.IsType<UnsetSetting>(context.PreparedDraft.Configuration.Fields.Read(rectangle));
                Assert.IsType<ReferenceSetting>(consumer.Configuration.Fields.Read(rectangle));
                Assert.True(creation.TrySetConstant(0L));
                Assert.True(context.TryCommitEditor(out var error), error);
            }
            finally { UiRuntime.Await(window.CloseAndDrainAsync()); }

            Assert.Equal("Local", consumer.Configuration.Fields.Get<string>(mode));
            Assert.IsType<UnsetSetting>(consumer.Configuration.Fields.Read(rectangle));
            Assert.True(fixture.Session.Undo());
            Assert.Equal("Rectangle", consumer.Configuration.Fields.Get<string>(mode));
            Assert.IsType<ReferenceSetting>(consumer.Configuration.Fields.Read(rectangle));
            Assert.True(fixture.Session.Redo());
            Assert.IsType<UnsetSetting>(consumer.Configuration.Fields.Read(rectangle));

            fixture.Save();
            var reopened = ServiceInitializer.SolutionManager.Open(
                Path.Combine(fixture.DirectoryPath, "document.solution"),
                _ => new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>()),
                _ => null, new System.Text.Json.JsonSerializerOptions());
            var reloaded = reopened.FindNode(fixture.Root.Id, consumer.Id)!;
            Assert.Equal("Local", reloaded.Configuration.Fields.Get<string>(mode));
            Assert.IsType<UnsetSetting>(reloaded.Configuration.Fields.Read(rectangle));
        });
    }

    [Fact]
    public void Reset_uses_owned_native_dialogs_and_only_confirmation_changes_the_draft()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
            using var fixture = new DocumentUiFixture();
            var source = fixture.Add();
            var blob = fixture.Add("BlobAnalysis", 350, 240);
            fixture.ConfigureImage(source, fixture.Image());
            fixture.Connect(source, blob);
            fixture.Fields(blob, (threshold, new ConstantSetting(150L)));
            fixture.Main.SelectedNode = blob;
            fixture.Main.OpenDebugWindowCommand.Execute(blob);
            UiRuntime.Pump();
            var window = System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
            try
            {
                UiRuntime.AwaitEditorReady(window);
                var context = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
                var settings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(window.Editor));
                var frame = Assert.IsType<NativeToolEditorFrame>(window.Content);
                Assert.Equal(Visibility.Collapsed, frame.ResetVisibility);
                var pageChanges = 0;
                window.Editor.ParameterPageChanged += () => pageChanges++;
                settings.SelectedPageIndex = 1;
                window.UpdateLayout();
                UiRuntime.Pump();
                Assert.Equal(1, pageChanges);
                var combo = UiRuntime.Visuals<ComboBox>(settings).First(item => item.IsVisible);
                combo.RaiseEvent(new SelectionChangedEventArgs(
                    System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
                Assert.Equal(1, pageChanges);
                var reset = frame.ResetCommand!;
                var resetButton = UiRuntime.Visuals<Button>(frame).Single(button => ReferenceEquals(button.Command, reset));
                Assert.True(resetButton.IsVisible);
                Assert.Equal(AppLocalizationService.GetLang("Lang_Reset parameters"), resetButton.ToolTip);
                var tabs = Assert.IsType<TabControl>(Assert.IsType<DeclaredFieldsView>(settings.Content).Content);
                Assert.IsType<TabItem>(tabs.SelectedItem).Header = "Run Params";
                Assert.True(reset.CanExecute(null));
                var dialogs = new List<NativeMessageBox>();
                void Answer(string button, Action? beforeClick = null)
                {
                    window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        var dialog = window.OwnedWindows.OfType<NativeMessageBox>().SingleOrDefault(item => item.IsVisible);
                        if (dialog is null) return;
                        dialogs.Add(dialog);
                        if (dialog.FindName(button) is not Button action) { dialog.Close(); return; }
                        beforeClick?.Invoke();
                        action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                }
                Answer("NoBtn");
                reset.Execute(null);
                Assert.Single(dialogs);
                Assert.Equal(150L, context.PreparedDraft.Configuration.Fields.Get<long>(threshold));

                Answer("YesBtn", () => Answer("OkBtn"));
                reset.Execute(null);
                Assert.Equal(3, dialogs.Count);
                Assert.All(dialogs, dialog => Assert.False(dialog.IsVisible));
                Assert.Equal(100L, context.PreparedDraft.Configuration.Fields.Get<long>(threshold));
                Assert.Equal(150L, blob.Configuration.Fields.Get<long>(threshold));
                settings.SelectedPageIndex = 2;
                UiRuntime.Pump();
                Assert.Equal(Visibility.Collapsed, frame.ResetVisibility);
                Assert.False(reset.CanExecute(null));
            }
            finally { UiRuntime.Await(window.CloseAndDrainAsync()); }
        });
    }

    [Fact]
    public void Disabling_position_correction_clears_its_source_in_the_same_undoable_edit()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var fixture=new DocumentUiFixture();
            var node=fixture.Add("BlobAnalysis",350,240);
            var source=fixture.Add();
            fixture.ConfigureImage(source,fixture.Image(180));
            fixture.Connect(source,node);
            const string correctionEnabled="field:c95f3a26edf55097863a7ec47d17cce5";
            var point=new RecordValue(FixtureDeclarations.Point,new Dictionary<string,object?> {["x"]=0d,["y"]=0d});
            var position=new RecordValue(FixtureDeclarations.Pose,new Dictionary<string,object?>
                {["point"]=point,["angle"]=0d,["scale-x"]=1d,["scale-y"]=1d});
            var pose=new RecordValue(FixtureDeclarations.Info,new Dictionary<string,object?> {["reference"]=position,["run"]=position});
            fixture.Fields(node,("input.pose",new ConstantSetting(pose)),
                (correctionEnabled,new ConstantSetting(true)));
            fixture.Main.SelectedNode=node;fixture.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
            var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();UiRuntime.AwaitEditorReady(window);
            var control=window.Editor;
            var settings=Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(control));
            settings.SelectedPageIndex=0;window.UpdateLayout();UiRuntime.Pump();
            var assist=Assert.Single(UiRuntime.Visuals<NativeAssistUiDraftControls>(control));
            Assert.Equal(Visibility.Visible,assist.Visibility);
            string[] VisibleAssistLabels() => assist.Children.OfType<DeclaredFieldRow>()
                .Where(row => row.Visibility == Visibility.Visible)
                .Select(row => Assert.IsType<FieldRowModel>(row.Content).DisplayNameA).ToArray();
            Assert.Equal(new[] { "Choose Mode", "Fixture Info" }, VisibleAssistLabels());
            var poseEditor=UiRuntime.Visuals<DeclaredFieldRow>(assist).Single(item=>item.FieldId=="input.pose");
            Assert.Equal("Description_CorrectInfo", Assert.IsType<FieldRowModel>(poseEditor.Content).DescriptionA);
            var toggle=UiRuntime.Visuals<DeclaredFieldRow>(control).Single(item=>item.FieldId==correctionEnabled);
            Assert.IsType<ConstantSetting>(poseEditor.Setting);
            Assert.True(assist.TrySetMode(1));UiRuntime.Pump();
            Assert.Equal(new[] { "Choose Mode", "Fixtured Point", "InitAngle", "InitScaleX", "InitScaleY",
                "Unfixtured Point", "RunAngle", "RunScaleX", "RunScaleY" }, VisibleAssistLabels());
            var pointEditor=UiRuntime.Visuals<DeclaredFieldRow>(assist).Single(item=>item.FieldId=="input.pose.reference.point");
            Assert.IsType<ConstantSetting>(pointEditor.Setting);
            Assert.True(assist.TrySetMode(2));UiRuntime.Pump();
            Assert.Equal(new[] { "Choose Mode", "InitPointX", "InitPointY", "InitAngle", "InitScaleX", "InitScaleY",
                "RunPointX", "RunPointY", "RunAngle", "RunScaleX", "RunScaleY" }, VisibleAssistLabels());
            const string referenceX="input.pose.reference.point.x";
            Assert.IsType<ConstantSetting>(UiRuntime.Visuals<DeclaredFieldRow>(assist).Single(item=>item.FieldId==referenceX).Setting);
            Assert.True(assist.TrySetMode(0));UiRuntime.Pump();
            Assert.True(toggle.TrySetConstant(false));
            UiRuntime.Pump();
            Assert.Equal(Visibility.Collapsed,assist.Visibility);
            Assert.IsType<UnsetSetting>(poseEditor.Setting);
            var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(control));
            Assert.IsType<UnsetSetting>(context.PreparedDraft.Configuration.Fields.Read(referenceX));
            Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(referenceX));
            Assert.Throws<ArgumentException>(()=>context.PreparedDraft.ApplyFields(
                new Dictionary<string,ParameterSetting> {["input.pose"]=new ConstantSetting(pose)}));
            Assert.True(context.TryCommitEditor(out var error),error);
            Assert.IsType<UnsetSetting>(node.Configuration.Fields.Read(referenceX));
            Assert.True(window.CloseAndDrainAsync().GetAwaiter().GetResult());UiRuntime.Pump();
            Assert.ThrowsAny<Exception>(()=>control.ReadNumericModels(NumericModelReadScope.All));
            Assert.True(fixture.Session.Undo());
            Assert.Equal(true,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(correctionEnabled)).Value);
            Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(referenceX));
            Assert.True(fixture.Session.Redo());
            Assert.Equal(false,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read(correctionEnabled)).Value);
            Assert.IsType<UnsetSetting>(node.Configuration.Fields.Read(referenceX));
        });
    }

    private static void Capture(FrameworkElement view,string path)
    {
        view.UpdateLayout();
        var width=(int)Math.Ceiling(view.ActualWidth);var height=(int)Math.Ceiling(view.ActualHeight);
        Assert.True(width>0 && height>0,$"真实视图没有布局：{width}×{height}");
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(view);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(path);encoder.Save(stream);
    }

    [Fact]
    public void Real_three_pages_read_numeric_models_and_retire_with_the_actual_editor()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var fixture=new DocumentUiFixture();
            var source=fixture.Add();var node=fixture.Add("BlobAnalysis",350,240);
            fixture.ConfigureImage(source,fixture.Image(180,side:24));fixture.Connect(source,node);
            var output=Path.GetFullPath(Path.Combine(UiRuntime.ProductRoot,"..","..","_validation","blob-chain","visual",Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(output);
            fixture.Main.OpenDebugWindowCommand.Execute(node);
            UiRuntime.Pump();
            var defaults = Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
            const string threshold="field:43bda03261df55b88f963bd18f2ba4d9";
            try
            {
                UiRuntime.AwaitEditorReady(defaults);
                var defaultSettings = Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(defaults.Editor));
                defaultSettings.SelectedPageIndex = 1;
                defaults.UpdateLayout();
                UiRuntime.Pump();
                Assert.False(Assert.Single(UiRuntime.Visuals<NativeAdvancedPresenter>(defaults.Editor), item => item.IsVisible).IsExpanded);
                Capture(defaults, Path.Combine(output, "blob-run-default.png"));
                var numericRow = Assert.Single(UiRuntime.Visuals<DeclaredFieldRow>(defaults.Editor), row => row.FieldId == threshold);
                var numericInput = Assert.IsAssignableFrom<TextBox>(numericRow.InputElement);
                var draftContext = Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(defaults.Editor));
                var originalConfiguration = node.Configuration;
                numericInput.SetCurrentValue(TextBox.TextProperty, "101");
                Assert.True(RaiseKey(numericInput, Key.Enter, Keyboard.PreviewKeyDownEvent).Handled);
                UiRuntime.Pump();
                Assert.Equal(101L, draftContext.ReadCurrentParameter(threshold)?.Value);
                Assert.Same(originalConfiguration, node.Configuration);
                Assert.Null(defaults.Editor.CurrentDebugSnapshot);
                Assert.True(defaults.IsCurrent);
            }
            finally { UiRuntime.Await(defaults.CloseAndDrainAsync()); }
            using(var variables=fixture.Session.Editing.Host.BeginVariableEdit())
            {
                variables.Variables.Add(new GlobalVariable("阈值",125L,"Integer"));
                fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host,variables:variables));
            }
            var variable=fixture.Session.Document.GlobalVariables.Single(item=>item.Name=="阈值");
            fixture.Fields(node,(threshold,new VariableSetting(new VariableReference(fixture.Session.Document.Id,variable.Id,TypeRef.ScalarOf(ScalarKind.Int64)))));
            fixture.Run();
            var original=node.Configuration;
            fixture.Main.SelectedNode=node;
            fixture.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
            var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();UiRuntime.AwaitEditorReady(window);
            var control=window.Editor;
            var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(control));
            Assert.Equal(125L,context.ReadCurrentParameter(threshold)?.Value);
            var settings=Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(control));
            settings.SelectedPageIndex=0;window.UpdateLayout();UiRuntime.Pump();
            const string correctionEnabled="field:c95f3a26edf55097863a7ec47d17cce5";
            var toggle=UiRuntime.Visuals<DeclaredFieldRow>(control).Single(item=>item.FieldId==correctionEnabled);
            var assist=Assert.Single(UiRuntime.Visuals<NativeAssistUiDraftControls>(control));
            Assert.Equal(Visibility.Collapsed,assist.Visibility);
            Assert.True(toggle.TrySetConstant(true));UiRuntime.Pump();
            Assert.Equal(Visibility.Visible,assist.Visibility);
            Assert.True(assist.TrySetMode(1));UiRuntime.Pump();
            Assert.Contains(UiRuntime.Visuals<DeclaredFieldRow>(assist),item=>item.FieldId=="input.pose.run.point"&&item.Visibility==Visibility.Visible);
            Assert.True(toggle.TrySetConstant(false));UiRuntime.Pump();
            Assert.Equal(Visibility.Collapsed,assist.Visibility);
            settings.SelectedPageIndex=2;window.UpdateLayout();UiRuntime.Pump();
            Assert.False(settings.HasInvalidInput);
            settings.SelectedPageIndex=0;window.UpdateLayout();UiRuntime.Pump();
            void ShowPage(int page)
            {
                settings.SelectedPageIndex=page;
                window.UpdateLayout();
                UiRuntime.Pump();
            }
            ScrollViewer PageScroll()=>UiRuntime.Visuals<ScrollViewer>(control).First(item=>item.IsVisible&&item.ScrollableHeight>0);
            NativeAdvancedPresenter Advanced()=>UiRuntime.Visuals<NativeAdvancedPresenter>(control).First(item=>item.IsVisible);
            void Shot(string name)=>Capture(window,Path.Combine(output,name+".png"));
            void ScrollTo(ScrollViewer scroll,double fraction)
            {
                scroll.ScrollToVerticalOffset(scroll.ScrollableHeight*fraction);
                window.UpdateLayout();
                UiRuntime.Pump();
            }
            try
            {
                for(var page=0;page<3;page++)
                {
                    ShowPage(page);
                    Assert.Equal(page,settings.SelectedPageIndex);
                    var pageReads=control.ReadNumericModels(NumericModelReadScope.CurrentPage);
                    Assert.DoesNotContain(pageReads,read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);
                    if(page!=0)Assert.False(Advanced().IsExpanded);
                    Shot($"blob-page{page}");
                }

                ShowPage(0);
                var sourceRow = UiRuntime.Visuals<DeclaredFieldRow>(control).Single(row => row.FieldId == "input.image");
                var sourcePicker = Assert.IsType<ComboBox>(sourceRow.InputElement);
                Assert.IsType<ReferenceSetting>(sourcePicker.SelectedValue);
                sourcePicker.IsDropDownOpen = true;
                try
                {
                    UiRuntime.Pump();
                    var popup = Assert.IsType<System.Windows.Controls.Primitives.Popup>(sourcePicker.Template.FindName("Popup", sourcePicker));
                    Assert.True(popup.IsOpen);
                    Capture(Assert.IsAssignableFrom<FrameworkElement>(popup.Child), Path.Combine(output, "blob-image-source-popup.png"));
                    RaiseKey(sourcePicker, Key.Escape, Keyboard.KeyDownEvent);
                    Assert.False(sourcePicker.IsDropDownOpen);
                    Assert.True(window.IsCurrent);
                    Assert.Null(control.CurrentDebugSnapshot);
                }
                finally { sourcePicker.IsDropDownOpen = false; }

                var roiArea=UiRuntime.Visuals<DeclaredFieldRow>(control).Single(row=>row.FieldId=="input.roi.area-type");
                Assert.True(roiArea.TrySetConstant(1L));
                UiRuntime.Pump();
                Assert.Contains(UiRuntime.Visuals<DeclaredFieldRow>(control),row=>row.FieldId=="input.roi.mask-image"&&row.IsVisible);
                Shot("blob-image-roi");
                Assert.True(roiArea.TrySetConstant(0L));

                ShowPage(2);
                var resultAdvanced=Advanced();
                resultAdvanced.IsExpanded=true;window.UpdateLayout();UiRuntime.Pump();
                resultAdvanced.BringIntoView();window.UpdateLayout();UiRuntime.Pump();
                var resultScroll=PageScroll();
                Shot("blob-result-advanced-upper");
                ScrollTo(resultScroll,.55);
                Shot("blob-result-advanced-lower");
                resultAdvanced.IsExpanded=false;window.UpdateLayout();UiRuntime.Pump();
                ScrollTo(resultScroll,.55);
                Shot("blob-result-image");
                ScrollTo(resultScroll,1);
                Shot("blob-result-text");

                ShowPage(1);
                var advanced=Advanced();
                advanced.IsExpanded=true;window.UpdateLayout();UiRuntime.Pump();
                advanced.BringIntoView();window.UpdateLayout();UiRuntime.Pump();
                var scroll=PageScroll();
                Shot("blob-advanced-upper");
                ScrollTo(scroll,1);
                Assert.True(scroll.VerticalOffset>0);
                Shot("blob-advanced-bottom");
                const string overlap = "field:1f1624765afb42d280d8090aa4c0a7db";
                const string outsideEnabled = "field:54f22aab059546c396046927f0d9f7ba";
                const string outsideLimit = "field:cd013f061e38431ebdb31252f4001a92";
                var outsideToggle = UiRuntime.Visuals<DeclaredFieldRow>(control).Single(row => row.FieldId == outsideEnabled && row.IsVisible);
                Assert.DoesNotContain(UiRuntime.Visuals<DeclaredFieldRow>(control), row => row.IsVisible && row.FieldId is overlap or outsideLimit);
                Assert.True(outsideToggle.TrySetConstant(true));
                window.UpdateLayout();scroll.ScrollToEnd();UiRuntime.Pump();
                Assert.Contains(UiRuntime.Visuals<DeclaredFieldRow>(control), row => row.FieldId == outsideLimit && row.IsVisible);
                Assert.DoesNotContain(UiRuntime.Visuals<DeclaredFieldRow>(control), row => row.FieldId == overlap && row.IsVisible);
                Assert.True(outsideToggle.TrySetConstant(false));
                advanced.IsExpanded=false;
                const string sortFeature="field:8ca20000000040008000000000000004";
                Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting>
                {
                    [sortFeature]=new ConstantSetting("SortFeatureAxisRatio")
                }));
                var all=control.ReadNumericModels(NumericModelReadScope.All);
                Assert.NotEmpty(all);
                Assert.DoesNotContain(all,read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);

                var field=control.ReadNumericModels(NumericModelReadScope.Field,threshold);
                Assert.Single(field);Assert.Equal(threshold,field[0].FieldId);
                Assert.True(field[0].Status is NumericModelReadStatus.Updated or NumericModelReadStatus.Unchanged);
                Assert.Same(original,node.Configuration);
                var previousDraft=context.PreparedDraft;
                Assert.True(context.TryCommitEditor(out var error),error);
                Assert.NotSame(previousDraft,context.PreparedDraft);
                Assert.Equal("SortFeatureAxisRatio",
                    Assert.IsType<ConstantSetting>(context.PreparedDraft.Configuration.Fields.Read(sortFeature)).Value);
                Assert.DoesNotContain(control.ReadNumericModels(NumericModelReadScope.All),read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);
                Assert.Equal("SortFeatureAxisRatio",node.Configuration.Fields.Get<string>(sortFeature));
                UiRuntime.Await(control.RunDebugAsync());
                var debug=control.CurrentDebugSnapshot;
                Assert.True(debug?.IsSuccess,debug?.ErrorMessage);
                IReadOnlyImage privateImage;
                using (var reader = debug!.AcquireReader())
                    privateImage = reader.Values!.Get<RecordValue>("field:8ca10000000040008000000000000006").Get<IReadOnlyImage>("data");
                Capture(fixture.Host,Path.Combine(output,"blob-debug-workspace.png"));
                var close=window.CloseAndDrainAsync();
                UiRuntime.Await(close);
                Assert.True(close.Result);
                Assert.ThrowsAny<Exception>(()=>control.ReadNumericModels(NumericModelReadScope.All));
                Assert.False(context.IsActive);
                Assert.Null(control.Content);
                Assert.Null(control.CurrentDebugSnapshot);
                Assert.Throws<ObjectDisposedException>(() => privateImage.CopyPixels());
                var activeWindows=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Count();
                Assert.Equal(0,activeWindows);
                Capture(fixture.Host,Path.Combine(output,"blob-after-close.png"));
                File.WriteAllText(Path.Combine(output,"close-state.json"),System.Text.Json.JsonSerializer.Serialize(new
                {
                    DebugInvocation=debug!.Identity,
                    WindowClosed=!window.IsCurrent,
                    DraftReleased=!context.IsActive,
                    ParameterViewReleased=control.Content is null,
                    DebugSnapshotReleased=control.CurrentDebugSnapshot is null,
                    PrivateImageReleased=true,
                    ActiveEditorWindows=activeWindows
                },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));

                fixture.Main.OpenDebugWindowCommand.Execute(source);
                UiRuntime.Pump();
                var imageSource=Assert.Single(System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>());
                UiRuntime.AwaitEditorReady(imageSource);
                Capture(imageSource,Path.Combine(output,"image-source.png"));
                UiRuntime.Await(imageSource.CloseAndDrainAsync());
            }
            finally { if(window.IsVisible)window.Close(); }
        });
    }
}
