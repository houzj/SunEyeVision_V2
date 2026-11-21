using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.DivideImage.Runtime;
using SunEyeVision.Tool.DivideImage.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>
/// 呈现层契约（P1/P2/P5）：参数页必须实现就绪信号、绑定前自行声明输入区、关闭走组合式释放。
/// 这些是宿主运行期的真实调用路径，缺一项会在打开参数页时抛异常。
/// </summary>
public sealed class DivideImagePresentationTests
{
    [Fact]
    public void Parameter_editor_supplies_the_input_section_itself_and_reports_readiness()
    {
        RunSta(() =>
        {
            // 宿主按页工厂提供输入区内容但没给分组名：参数页必须自己补"基本参数"，
            // 否则 DeclaredSettingsView 抛 "An input content section is required."（今日 4 次运行期崩溃之一）。
            using var editor = new DivideImageParameterEditor(CreateDraft(), new ToolSettingsViewOptions
            {
                InputContentFactory = _ => new Border()
            });
            // P1：宿主据此判定参数页就绪。
            var ready = Assert.IsAssignableFrom<IToolSettingsViewReady>(editor);
            Assert.True(ready.ReadyTask.IsCompletedSuccessfully);
            Assert.False(editor.HasInvalidInput);
            Assert.Equal(new[]
            {
                DivideImageFields.PositionCorrectionEnabled,
                DivideImageFields.Columns, DivideImageFields.Rows, DivideImageFields.OverlapX, DivideImageFields.OverlapY
            }, editor.CaptureAcceptedFields().Declarations.Keys.ToArray());
        });
    }

    [Fact]
    public void Parameter_editor_keeps_a_host_supplied_input_section()
    {
        RunSta(() =>
        {
            using var editor = new DivideImageParameterEditor(CreateDraft(), new ToolSettingsViewOptions
            {
                InputSection = DivideImageFields.Section,
                InputContentFactory = _ => new Border()
            });
            Assert.False(editor.HasInvalidInput);
            Assert.Equal(5, editor.CaptureAcceptedFields().Declarations.Count);
        });
    }

    [Fact]
    public void Parameter_editor_refuses_rebinding_after_dispose()
    {
        RunSta(() =>
        {
            var editor = new DivideImageParameterEditor(CreateDraft(), new ToolSettingsViewOptions());
            editor.Dispose();
            // P5：组合式释放后不再接受绑定或读取。
            Assert.Throws<ObjectDisposedException>(() => editor.Bind(CreateDraft(), new ToolSettingsViewOptions()));
            Assert.Throws<ObjectDisposedException>(() => editor.ReadNumericModels(NumericModelReadScope.All));
        });
    }

    private static FieldSettingsDraft CreateDraft()
    {
        var tool = new DivideImageTool();
        var schema = ResultDisplayFields.EditorSchema(tool);
        var defaults = ((IToolSettingsFields)tool).ReadSettings(new DivideImageSettings()).CopyValues()
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value),
                StringComparer.Ordinal);
        return new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, defaults));
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
