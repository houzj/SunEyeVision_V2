using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量原厂 AssistGroup（位置修正）。模式、来源行与显隐由公共 Assist 控件负责，
/// 工具只声明字段身份。证据：IMVSIntensityMeasureModuAlgorithmTab.xml 的 AssistGroup。
/// </summary>
public static class IntensityMeasureAssist
{
    public const string ModeId = "input.pose.mode";
    public const string Information = "input.pose.information";
    public const string BasePoint = "input.pose.base-point";
    public const string RunPoint = "input.pose.run-point";

    public static TypeRef PointType { get; } = TypeRef.RecordOf("intensitymeasure.pose.point", new[]
    {
        new Port("x", DataType.Number), new Port("y", DataType.Number)
    });

    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(ModeId, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new Port(Information, DataType.Text, Required: false, EditModes: FieldEditModes.Reference),
        new Port(BasePoint, PointType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(RunPoint, PointType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference)
    }
    .Concat(new[] { "x", "y" }.Select(id => new Port(BasePoint + "." + id, DataType.Number,
        Required: false, EditModes: FieldEditModes.Reference)))
    .Concat(new[] { "x", "y" }.Select(id => new Port(RunPoint + "." + id, DataType.Number,
        Required: false, EditModes: FieldEditModes.Reference)))
    .Concat(new[] { "angle", "scale-x", "scale-y" }.SelectMany(id => new[]
    {
        new Port(BasePoint + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference),
        new Port(RunPoint + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)
    }))
    .ToArray();

    public static NativeAssistUiDraftControls Add(DeclaredInputFieldsView inputs,
        Action<FrameworkElement> attach)
    {
        var mode = inputs.ComposeInputEditor(ModeId, "选择方式",
            defaultWhenUnset: new ConstantSetting(0L),
            integerRadioOptions: new NativeIntegerRadioOptions(new[]
            {
                new NativeIntegerRadioChoice(0, "按信息"),
                new NativeIntegerRadioChoice(1, "按点"),
                new NativeIntegerRadioChoice(2, "按坐标")
            }, useFiveSlotTemplate: true));
        var information = inputs.ComposeInputEditor(Information, "修正信息", referenceOnly: true);
        var sources = Sources(inputs).ToArray();
        var assist = new NativeAssistUiDraftControls(mode, information, sources);
        try
        {
            attach(assist);
            return assist;
        }
        catch
        {
            assist.Dispose();
            throw;
        }
    }

    private static IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> Sources(DeclaredInputFieldsView inputs)
    {
        (int, Func<DeclaredFieldRow>) Source(int mode, string id, string label)
            => (mode, () => inputs.ComposeInputEditor(id, label, referenceOnly: true));

        yield return Source(1, BasePoint, "基准点");
        yield return Source(2, BasePoint + ".x", "基准点X");
        yield return Source(2, BasePoint + ".y", "基准点Y");
        yield return Source(3, BasePoint + ".angle", "基准角度");
        yield return Source(3, BasePoint + ".scale-x", "基准尺度X");
        yield return Source(3, BasePoint + ".scale-y", "基准尺度Y");
        yield return Source(1, RunPoint, "运行点");
        yield return Source(2, RunPoint + ".x", "运行点X");
        yield return Source(2, RunPoint + ".y", "运行点Y");
        yield return Source(3, RunPoint + ".angle", "运行角度");
        yield return Source(3, RunPoint + ".scale-x", "运行尺度X");
        yield return Source(3, RunPoint + ".scale-y", "运行尺度Y");
    }
}
