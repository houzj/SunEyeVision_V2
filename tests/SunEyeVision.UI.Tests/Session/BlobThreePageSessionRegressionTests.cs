using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Controls.References.Primitives;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Services.Editing;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class BlobThreePageSessionRegressionTests
{
    [Fact]
    public void Disabled_parent_keeps_existing_pose_clearable_and_retires_its_view()
    {
        if (System.Windows.Application.Current is null) SunEyeVision.TestShared.WpfTestDispatcher.RequireHiddenDesktop();
        UiRuntime.Run(() =>
        {
            using var fixture=new DocumentUiFixture();
            var node=fixture.Add("BlobAnalysis",350,240);
            var pose=new RecordValue(SpatialDeclarations.Affine2D,new Dictionary<string,object?> {
                ["m11"]=1d,["m12"]=0d,["m21"]=0d,["m22"]=1d,
                ["offset-x"]=0d,["offset-y"]=0d,["from"]="image",["to"]="image"});
            fixture.Fields(node,("input.pose",new ConstantSetting(pose)),
                ("field:c95f3a26edf55097863a7ec47d17cce5",new ConstantSetting(false)));
            fixture.Main.SelectedNode=node;fixture.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
            var window=System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();UiRuntime.AwaitEditorReady(window);
            var control=window.Editor;
            var settings=Assert.Single(UiRuntime.Visuals<DeclaredSettingsView>(control));
            settings.SelectedPageIndex=0;window.UpdateLayout();UiRuntime.Pump();
            var assist=Assert.Single(UiRuntime.Visuals<NativeAssistUiDraftControls>(control));
            Assert.Equal(Visibility.Visible,assist.Visibility);
            var poseEditor=UiRuntime.Visuals<DeclaredFieldRow>(assist).Single(item=>item.FieldId=="input.pose");
            poseEditor.BringIntoView();window.UpdateLayout();UiRuntime.Pump();
            var text=Assert.Single(UiRuntime.Visuals<DeleteTextBox>(poseEditor));
            text.Focus();UiRuntime.Pump();
            text.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(text),0,Key.Delete){RoutedEvent=Keyboard.KeyUpEvent});
            text.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            UiRuntime.Pump();
            Assert.True(assist.Visibility==Visibility.Collapsed,
                $"assist={assist.Visibility}, pose={poseEditor.Setting.GetType().Name}, text={text.Text ?? "<null>"}, focus={text.IsKeyboardFocusWithin}, readOnly={text.IsReadOnly}");
            var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(control));
            Assert.True(context.TryCommitEditor(out var error),error);
            Assert.IsType<UnsetSetting>(node.Configuration.Fields.Read("input.pose"));
            Assert.True(window.CloseAndDrainAsync().GetAwaiter().GetResult());UiRuntime.Pump();
            Assert.ThrowsAny<Exception>(()=>control.ReadNumericModels(NumericModelReadScope.All));
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
            fixture.ConfigureImage(source,fixture.Image(180));fixture.Connect(source,node);
            using(var variables=fixture.Session.Editing.Host.BeginVariableEdit())
            {
                variables.Variables.Add(new GlobalVariable("阈值",125L,"Integer"));
                fixture.Session.CommitEdit(DocumentEditProposal.Prepare(fixture.Session.Editing.Host,variables:variables));
            }
            var variable=fixture.Session.Document.GlobalVariables.Single(item=>item.Name=="阈值");
            const string threshold="field:43bda03261df55b88f963bd18f2ba4d9";
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
            Assert.True(assist.IsUnimplementedMode);
            Assert.True(toggle.TrySetConstant(false));UiRuntime.Pump();
            Assert.Equal(Visibility.Collapsed,assist.Visibility);
            settings.SelectedPageIndex=2;window.UpdateLayout();UiRuntime.Pump();
            Assert.False(settings.HasInvalidInput);
            settings.SelectedPageIndex=0;window.UpdateLayout();UiRuntime.Pump();
            var output=Path.GetFullPath(Path.Combine(UiRuntime.ProductRoot,"..","..","_validation","blob-chain","visual"));
            Directory.CreateDirectory(output);
            try
            {
                for(var page=0;page<3;page++)
                {
                    settings.SelectedPageIndex=page;window.UpdateLayout();UiRuntime.Pump();
                    Assert.Equal(page,settings.SelectedPageIndex);
                    var pageReads=control.ReadNumericModels(NumericModelReadScope.CurrentPage);
                    Assert.DoesNotContain(pageReads,read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);
                    Capture(window,Path.Combine(output,$"blob-page{page}.png"));
                }
                settings.SelectedPageIndex=1;window.UpdateLayout();UiRuntime.Pump();
                var advanced=UiRuntime.Visuals<NativeAdvancedPresenter>(control).FirstOrDefault(item=>item.IsVisible);
                Assert.NotNull(advanced);advanced.IsExpanded=true;window.UpdateLayout();UiRuntime.Pump();
                advanced.BringIntoView();window.UpdateLayout();UiRuntime.Pump();
                var scroll=UiRuntime.Visuals<ScrollViewer>(control).FirstOrDefault(item=>item.ScrollableHeight>0);
                Assert.NotNull(scroll);scroll.ScrollToEnd();window.UpdateLayout();UiRuntime.Pump();
                Assert.True(scroll.VerticalOffset>0);
                Capture(window,Path.Combine(output,"blob-advanced-bottom.png"));
                advanced.IsExpanded=false;
                var all=control.ReadNumericModels(NumericModelReadScope.All);
                Assert.NotEmpty(all);
                Assert.DoesNotContain(all,read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);

                var field=control.ReadNumericModels(NumericModelReadScope.Field,threshold);
                Assert.Single(field);Assert.Equal(threshold,field[0].FieldId);
                Assert.True(field[0].Status is NumericModelReadStatus.Updated or NumericModelReadStatus.Unchanged);
                var previousDraft=context.PreparedDraft;
                Assert.True(context.TryCommitEditor(out var error),error);
                Assert.NotSame(previousDraft,context.PreparedDraft);
                Assert.DoesNotContain(control.ReadNumericModels(NumericModelReadScope.All),read=>read.Status is NumericModelReadStatus.Failed or NumericModelReadStatus.Retired);
                Assert.Same(original,node.Configuration);
                Assert.True(window.CloseAndDrainAsync().GetAwaiter().GetResult());UiRuntime.Pump();
                Assert.ThrowsAny<Exception>(()=>control.ReadNumericModels(NumericModelReadScope.All));
            }
            finally { if(window.IsVisible)window.Close(); }
        });
    }
}
