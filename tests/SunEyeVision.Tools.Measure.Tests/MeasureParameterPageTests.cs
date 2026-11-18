using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.CircleCircleMeasure.Runtime;
using SunEyeVision.Tool.CircleCircleMeasure.Wpf.Views;
using SunEyeVision.Tool.PointPointMeasure.Runtime;
using SunEyeVision.Tool.PointPointMeasure.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.Measure.Tests;

/// <summary>
/// 参数页组装闭环：按运行期同一声明链构造两个工具的参数页，验证 P1~P5 呈现契约
/// 在真实控件组合时不抛异常（宿主日志对应 ToolWindow stage=CreatePresentation，无「参数页组装失败」）。
/// WPF 控件必须在 STA 线程构造，xUnit 默认 MTA，因此测试体在专用 STA 线程执行。
/// </summary>
public sealed class MeasureParameterPageTests
{
    private static void RunOnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));
        if (failure is not null) throw failure;
    }

    private static (FieldSettingsDraft Draft, ToolSettingsViewOptions Options) Context(
        IToolDefinition tool, Func<ToolSettingsViewOptions, ToolSettingsViewOptions> configure, string inputSection)
    {
        var fields = tool switch
        {
            CircleCircleMeasureTool => (IToolSettingsFields)new CircleCircleMeasureTool(),
            _ => (IToolSettingsFields)new PointPointMeasureTool()
        };
        var settings = tool switch
        {
            CircleCircleMeasureTool => fields.ReadSettings(new CircleCircleMeasureSettings()),
            _ => fields.ReadSettings(new PointPointMeasureSettings())
        };
        var display = tool switch
        {
            CircleCircleMeasureTool => new CircleCircleMeasureTool().CreateDisplaySettings(),
            _ => new PointPointMeasureTool().CreateDisplaySettings()
        };
        var schema = ResultDisplayFields.EditorSchema(tool);
        var values = settings.CopyValues().Concat(display.CopyValues())
            .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
        var draft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, values));
        return (draft, configure(new ToolSettingsViewOptions(InputSection: inputSection)));
    }

    [Fact]
    public void CircleCircle_parameter_page_assembles_with_all_tabs_and_display_groups()
        => RunOnSta(() =>
        {
            var (draft, options) = Context(new CircleCircleMeasureTool(),
                CircleCircleMeasureDisplayUi.Configure, CircleCircleMeasureFields.BasicPage);
            using var view = new CircleCircleMeasureParameterEditor(draft, options);
            Assert.True(view.ReadyTask.IsCompletedSuccessfully);
            Assert.True(view.TryCapture(out var fields, out var error), error);
            Assert.Equal(ResultDisplayFields.EditorSchema(new CircleCircleMeasureTool()).Inputs.Count, fields.Capture().Count);
        });

    [Fact]
    public void PointPoint_parameter_page_assembles_with_all_tabs_and_display_groups()
        => RunOnSta(() =>
        {
            var (draft, options) = Context(new PointPointMeasureTool(),
                PointPointMeasureDisplayUi.Configure, PointPointMeasureFields.BasicPage);
            using var view = new PointPointMeasureParameterEditor(draft, options);
            Assert.True(view.ReadyTask.IsCompletedSuccessfully);
            Assert.True(view.TryCapture(out var fields, out var error), error);
            Assert.Equal(ResultDisplayFields.EditorSchema(new PointPointMeasureTool()).Inputs.Count, fields.Capture().Count);
        });
}
