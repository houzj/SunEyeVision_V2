using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.PointPointMeasure.Runtime;

namespace SunEyeVision.Tool.PointPointMeasure.Wpf.Views;

/// <summary>结果显示页组合：原厂 LineResult/OriginPointInput/EndPointInput 三组图形与 ResutlText 文本。</summary>
public static class PointPointMeasureDisplayUi
{
    public static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => PointPointMeasureResultDisplay.Field(group, role);
        NativeGraphicDisplayOptions Graphic(string group)
            => new(group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width"));
        var text = PointPointMeasureResultDisplay.Text;
        return options with
        {
            GraphicDisplays = PointPointMeasureResultDisplay.DisplayGroups.Select(Graphic).ToArray(),
            TextDisplays = new[]
            {
                new NativeTextDisplayOptions(
                    Id(text, "content"), Id(text, "ok"), Id(text, "ng"), Id(text, "size"),
                    Id(text, "opacity"), Id(text, "x"), Id(text, "y"))
            }
        };
    }
}
