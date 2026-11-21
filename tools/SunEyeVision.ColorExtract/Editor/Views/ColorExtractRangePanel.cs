using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ColorExtract.Runtime;

namespace SunEyeVision.Tool.ColorExtract.Wpf.Views;

/// <summary>
/// 颜色抽取运行参数接缝：九字段区间的序列化与提交由工具负责，
/// 卡片列表、颜色空间、三通道直方图/滑条和颜色反转使用共享 <see cref="NativeColorRangeList"/>
/// （原厂 ColorExtractList + UserScrollView）。
/// </summary>
public sealed class ColorExtractRangePanel : UserControl
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly NativeColorRangeList _list = new();
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, FontSize = 12 };
    private bool _loading;

    public ColorExtractRangePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _list.RangesChanged += (_, _) => ApplyRanges();
        _list.Rejected += (_, e) => _error.Text = e.Message;
        var root = new DockPanel();
        DockPanel.SetDock(_error, Dock.Bottom);
        root.Children.Add(_error);
        root.Children.Add(_list);
        Content = root;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    private void Reload()
    {
        _loading = true;
        try
        {
            var ranges = ColorExtractRanges.Parse(ReadText());
            _list.LoadValues(ranges.Select(ToShared).ToArray());
            _error.Text = string.Empty;
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        catch (Exception error) { _error.Text = $"颜色范围面板组装失败：{error.GetType().Name}: {error.Message}"; }
        finally { _loading = false; }
    }

    private string ReadText()
        => _draft.CaptureFields().Read(ColorExtractFields.ColorRange) is ConstantSetting { Value: string text }
            ? text
            : ColorExtractRanges.DefaultSerialized;

    private void ApplyRanges()
    {
        if (_loading) return;
        try
        {
            var encoded = ColorExtractRanges.Format(_list.ReadValues().Select(ToTool));
            _draft.ApplyFields(
                new System.Collections.Generic.Dictionary<string, ParameterSetting>(StringComparer.Ordinal)
                {
                    [ColorExtractFields.ColorRange] = new ConstantSetting(encoded)
                },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            _error.Text = string.Empty;
        }
        catch (ArgumentException error) { _error.Text = error.Message; }
        catch (Exception error) { _error.Text = $"颜色范围提交失败：{error.GetType().Name}: {error.Message}"; }
    }

    private static ColorRangeValue ToShared(ColorExtractRange range) => new(
        range.C1High, range.C1Low, range.C2High, range.C2Low, range.C3High, range.C3Low,
        (ColorRangeSpace)range.ColorSpace, range.IsColorInversion, range.IsChecked);

    private static ColorExtractRange ToTool(ColorRangeValue value) => new(
        value.C1High, value.C1Low, value.C2High, value.C2Low, value.C3High, value.C3Low,
        (ColorExtractColorSpace)value.ColorSpace, value.IsColorInversion, value.IsChecked);
}
