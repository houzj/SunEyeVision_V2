using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.CircleCircleMeasure.Runtime;

namespace SunEyeVision.Tool.CircleCircleMeasure.Wpf.Views;

/// <summary>
/// 结果显示页的控件组合声明。原厂 ImageViewConfig 九组图形各五行与文本七行由这里完整消费
/// （缺一组或缺一行都会被参数页组装拒绝）；FixtureInitPoint/FixtureRunPoint 未取证不实现。
/// </summary>
public static class CircleCircleMeasureDisplayUi
{
    public static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => CircleCircleMeasureResultDisplay.Field(group, role);
        NativeGraphicDisplayOptions Graphic(string group)
            => new(group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width"));
        var text = CircleCircleMeasureResultDisplay.Text;
        return options with
        {
            GraphicDisplays = CircleCircleMeasureResultDisplay.DisplayGroups.Select(Graphic).ToArray(),
            TextDisplays = new[]
            {
                new NativeTextDisplayOptions(
                    Id(text, "content"), Id(text, "ok"), Id(text, "ng"), Id(text, "size"),
                    Id(text, "opacity"), Id(text, "x"), Id(text, "y"))
            }
        };
    }
}
