using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;
using static SunEyeVision.Tool.ContourMatch.Runtime.ContourMatchFixtureControl;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 位置修正 UI 装配，按原厂四状态实机取证（docs/evidence/vm-contourmatch-ui-20261121/）：
/// 开关 + 选择方式（按信息/按点/按坐标）+ 各模式输入行（引用行无清除按钮）。
/// 复用平台 NativeAssistUiDraftControls；字段 ID 按模式分组且唯一（避免同实例重复挂载崩溃）。
/// </summary>
internal static class ContourMatchFixtureComposer
{
    public static DeclaredFieldRow ComposeSwitch(DeclaredInputFieldsView inputs)
        => inputs.ComposeInputEditor(EnabledId, "位置修正",
            defaultWhenUnset: new ConstantSetting(false), showClearButton: false);

    public static NativeAssistUiDraftControls Add(DeclaredInputFieldsView inputs, Action<FrameworkElement> attach)
    {
        var mode = inputs.ComposeInputEditor(ModeId, "选择方式",
            defaultWhenUnset: new ConstantSetting(0L), showClearButton: false,
            integerRadioOptions: new NativeIntegerRadioOptions(new[]
            {
                new NativeIntegerRadioChoice(0, "按信息"),
                new NativeIntegerRadioChoice(1, "按点"),
                new NativeIntegerRadioChoice(2, "按坐标")
            }, useFiveSlotTemplate: true));
        var information = inputs.ComposeInputEditor(Information, "修正信息", referenceOnly: true, showClearButton: false);
        var assist = new NativeAssistUiDraftControls(mode, information, Sources(inputs).ToArray());
        try { attach(assist); return assist; }
        catch { assist.Dispose(); throw; }
    }

    private static IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> Sources(DeclaredInputFieldsView inputs)
    {
        (int, Func<DeclaredFieldRow>) Ref(int mode, string id, string label)
            => (mode, () => inputs.ComposeInputEditor(id, label, referenceOnly: true, showClearButton: false));

        // 按点（mode=1）：POINT 引用 + 角度/尺度（8 行，ID 唯一）
        yield return Ref(1, PointBasePoint, "基准点");
        yield return Ref(1, PointBaseAngle, "基准角度");
        yield return Ref(1, PointBaseScaleX, "基准尺度X");
        yield return Ref(1, PointBaseScaleY, "基准尺度Y");
        yield return Ref(1, PointRunPoint, "运行点");
        yield return Ref(1, PointRunAngle, "运行角度");
        yield return Ref(1, PointRunScaleX, "运行尺度X");
        yield return Ref(1, PointRunScaleY, "运行尺度Y");

        // 按坐标（mode=2）：X/Y + 角度/尺度（10 行，ID 与按点不同）
        yield return Ref(2, CoordBaseX, "基准点X");
        yield return Ref(2, CoordBaseY, "基准点Y");
        yield return Ref(2, CoordBaseAngle, "基准角度");
        yield return Ref(2, CoordBaseScaleX, "基准尺度X");
        yield return Ref(2, CoordBaseScaleY, "基准尺度Y");
        yield return Ref(2, CoordRunX, "运行点X");
        yield return Ref(2, CoordRunY, "运行点Y");
        yield return Ref(2, CoordRunAngle, "运行角度");
        yield return Ref(2, CoordRunScaleX, "运行尺度X");
        yield return Ref(2, CoordRunScaleY, "运行尺度Y");
    }
}
