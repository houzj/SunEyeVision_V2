using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Tool.FrameMean.Wpf.Views;

/// <summary>
/// 帧均值运行参数接缝：命令语义、运行数量读取与回写由工具提供，
/// 按钮组合、样式与布局使用共享 <see cref="NativeCommandSelector"/>（原厂 CommandSelecter_Frame）。
/// </summary>
internal sealed class FrameMeanCommandPanel : UserControl, IToolSettingsRuntimeState
{
    private const string ResourceRoot =
        "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Composition/ParameterPresentation/resources/";

    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly Func<string, long?>? _readRuntimeInteger;
    private readonly NativeCommandSelector _selector = new();
    private bool _loading;

    public FrameMeanCommandPanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, Func<string, long?>? readRuntimeInteger)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _readRuntimeInteger = readRuntimeInteger;
        _selector.Options = new[]
        {
            new NativeCommandOption("1", "Record CurrentImage", ResourceRoot + "统计当前图像.png"),
            new NativeCommandOption("2", "NoRecord CurrentImage", ResourceRoot + "不统计当前图像-copy.png")
        };
        _selector.CountLabelKey = "已统计图像数量";
        _selector.ClearValue = "3";
        _selector.CommandSelected += (_, e) => Select(e.Value);
        Content = _selector;
        RefreshRuntimeState();
    }

    private void Select(string value)
    {
        if (_loading) return;
        _loading = true;
        try
        {
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting>(StringComparer.Ordinal) { ["proc.step"] = new ConstantSetting(value) },
                candidate => _acceptCandidate?.Invoke(candidate));
        }
        finally { _loading = false; }
    }

    public void RefreshRuntimeState()
    {
        _loading = true;
        try
        {
            var step = (_draft.CaptureFields().Read("proc.step") as ConstantSetting)?.Value as string ?? "1";
            _selector.SetSelected(step);
            _selector.SetCount(_readRuntimeInteger?.Invoke("output.image-count"));
        }
        finally { _loading = false; }
    }
}
