using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.IntensityMeasure.Runtime;

namespace SunEyeVision.Tool.IntensityMeasure.Wpf.Views;

/// <summary>
/// 结果显示页的控件组合声明：两个图形分组的五行样式与文本七行。
/// 分组内字段必须被这里完整消费（visible/ok/ng/opacity/width 缺一即被参数页拒绝）；
/// 文本位置两行的「不限固定小数位」写法必须注册为文本坐标，否则参数页拒绝组装。
/// </summary>
public static class IntensityMeasureDisplayUi
{
    public static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => IntensityMeasureResultDisplay.Field(group, role);
        NativeGraphicDisplayOptions Graphic(string group)
            => new(group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width"));
        var text = IntensityMeasureResultDisplay.Text;
        // 原厂结果显示页没有「亮度区域」行，也没有文本显示总开关行；
        // 两个字段仍保留以维持结果渲染/持久化，但不在页面上单独成行。
        var suppressed = new HashSet<string>(options.SuppressedFieldRows ?? new HashSet<string>(), StringComparer.Ordinal)
        {
            IntensityMeasureResultDisplay.Field(IntensityMeasureResultDisplay.Region, "visible")
        };
        return options with
        {
            SuppressedFieldRows = suppressed,
            VisibilityDisplays = new[]
            {
                new NativeVisibilityDisplayOptions(IntensityMeasureResultDisplay.TextOverlayVisible)
            },
            GraphicDisplays = new[]
            {
                Graphic(IntensityMeasureResultDisplay.Region),
                Graphic(IntensityMeasureResultDisplay.Roi),
                Graphic(IntensityMeasureResultDisplay.ReferencePoint),
                Graphic(IntensityMeasureResultDisplay.RunPoint),
                Graphic(IntensityMeasureResultDisplay.Annulus)
            },
            TextDisplays = new[]
            {
                new NativeTextDisplayOptions(
                    Id(text, "content"), Id(text, "ok"), Id(text, "ng"), Id(text, "size"),
                    Id(text, "opacity"), Id(text, "x"), Id(text, "y"))
            }
        };
    }
}
