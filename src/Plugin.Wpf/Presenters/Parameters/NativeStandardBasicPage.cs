using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>
/// 标准基本参数页声明：各工具只提供自己的字段身份、ROI 组合器和 Assist 组合器，
/// 行抑制、ROI 编辑宿主、位置修正开关与生命周期由平台统一完成。
/// </summary>
public sealed record NativeStandardBasicPageSpec
{
    /// <summary>ROI/Assist 端口身份；这些字段的参数行由组合控件代替，需从页面抑制。</summary>
    public required IReadOnlyList<string> RoiFieldIds { get; init; }
    /// <summary>「位置修正」开关字段；ROI 组插在它前面，Assist 组插在它后面。</summary>
    public required string PositionCorrectionFieldId { get; init; }
    public required Func<DeclaredInputFieldsView, Action<string, FrameworkElement, string?>, Action<NativeRoiUiDraftControls>?, NativeRoiUiDraftControls> ComposeRoi { get; init; }
    public required Func<DeclaredInputFieldsView, Action<FrameworkElement>, NativeAssistUiDraftControls> ComposeAssist { get; init; }
    public required Func<DeclaredInputFieldsView, INativeRoiEditDocument> CreateRoiDocument { get; init; }
    /// <summary>原厂 ROISelecter 的工具级形状白名单，空值保留公共控件默认集合。</summary>
    public IReadOnlyList<string>? RoiShapes { get; init; }
}

/// <summary>
/// 大多数工具共用的基本参数页装配：输入行 → ROI 区域（含位置修正开关）→ Assist 组。
/// 工具不再各自重复抑制行、创建 ROI 编辑宿主或接线开关。
/// </summary>
public sealed class NativeStandardBasicPage : IDisposable
{
    private readonly NativeRoiEditHost _roiHost;
    private readonly NativeAssistUiDraftControls _assist;
    private readonly NativeRoiUiDraftControls _roi;
    private readonly DeclaredFieldAccess? _positionCorrectionRow;
    private readonly Action _positionCorrectionChanged;
    private bool _disposed;

    private NativeStandardBasicPage(NativeRoiEditHost roiHost, NativeAssistUiDraftControls assist,
        NativeRoiUiDraftControls roi, DeclaredFieldAccess? positionCorrectionRow)
    {
        _roiHost = roiHost;
        _assist = assist;
        _roi = roi;
        _positionCorrectionRow = positionCorrectionRow;
        _positionCorrectionChanged = OnPositionCorrectionChanged;
        if (_positionCorrectionRow is not null)
            _positionCorrectionRow.Changed += _positionCorrectionChanged;
        ApplyPositionCorrection(ReadPositionCorrectionEnabled());
    }

    public static NativeStandardBasicPage Compose(DeclaredInputFieldsView inputs, DeclaredFieldsView fields,
        ToolSettingsViewOptions options, NativeStandardBasicPageSpec spec)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(spec);

        var redundant = fields.Fields
            .Where(field => spec.RoiFieldIds.Contains(field.FieldId, StringComparer.Ordinal))
            .Select(field => field.FieldId)
            .ToArray();
        if (redundant.Length != 0) fields.SuppressFieldRows(redundant);

        NativeRoiUiDraftControls? roi = null;
        var roiControls = spec.ComposeRoi(inputs, (section, content, group) =>
        {
            if (content is NativeRoiUiDraftControls)
                fields.AddAfterField(spec.PositionCorrectionFieldId, content,
                    followAnchorVisibility: false, insertBefore: true);
            else
                fields.AddToGroup(group ?? section, content);
        }, controls => roi = controls);
        if (roi is null) throw new InvalidOperationException("标准基本参数页：ROI 组未完成组装。");

        var host = new NativeRoiEditHost(inputs, roiControls, options.RoiFrames,
            spec.CreateRoiDocument(inputs), options.RoiDrawBlockReason, spec.RoiShapes);
        try
        {
            var assist = spec.ComposeAssist(inputs, content =>
                fields.AddAfterField(spec.PositionCorrectionFieldId, content,
                    followAnchorVisibility: false));
            var row = fields.Fields.FirstOrDefault(field =>
                string.Equals(field.FieldId, spec.PositionCorrectionFieldId, StringComparison.Ordinal));
            return new NativeStandardBasicPage(host, assist, roiControls, row);
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private bool ReadPositionCorrectionEnabled()
        => _positionCorrectionRow?.Setting is ConstantSetting { Value: true };

    private void OnPositionCorrectionChanged()
        => ApplyPositionCorrection(ReadPositionCorrectionEnabled());

    private void ApplyPositionCorrection(bool enabled)
    {
        if (_disposed) return;
        _roi.SetPositionCorrectionEnabled(enabled);
        _assist.SetPositionCorrectionEnabled(enabled);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_positionCorrectionRow is not null)
            _positionCorrectionRow.Changed -= _positionCorrectionChanged;
        _roiHost.Dispose();
        _assist.Dispose();
    }
}
