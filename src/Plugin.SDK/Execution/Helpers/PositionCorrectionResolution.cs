using System;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Helpers;

public enum PositionCorrectionState { Disabled, Valid, Unbound, WaitingForSource, EmptyMatch, InvalidMatrix, UnsupportedTransform, ImageFrameMismatch }

/// <summary>A value snapshot, separate from the editable binding reference. Does not retain an image or controls.</summary>
public sealed class PositionCorrectionResolution
{
    private readonly AffineMatrix? _matrix;
    private PositionCorrectionResolution(PositionCorrectionState state, string source, string message, AffineMatrix? matrix = null, bool framePairingVerified = false, Guid? correctionFrameId = null)
    { State = state; Source = source; Message = message; _matrix = Copy(matrix); FramePairingVerified = framePairingVerified; CorrectionFrameId = correctionFrameId; }
    public PositionCorrectionState State { get; }
    public string Source { get; }
    public string Message { get; }
    public bool CanUse => State is PositionCorrectionState.Disabled or PositionCorrectionState.Valid;
    public bool FramePairingVerified { get; }
    public Guid? CorrectionFrameId { get; }

    /// <summary>UI supplies identity only; execution has no dependency on WPF or a preview control.</summary>
    public PositionCorrectionResolution ForPreview(Guid? previewFrame, bool hasImage)
    {
        if (!CanUse || State == PositionCorrectionState.Disabled) return this;
        if (!hasImage) return new(PositionCorrectionState.WaitingForSource, Source, "当前预览没有图像，无法编辑位置修正图形。");
        if (CorrectionFrameId.HasValue && previewFrame.HasValue && CorrectionFrameId != previewFrame)
            return new(PositionCorrectionState.ImageFrameMismatch, Source, "当前预览图像与位置修正结果不是同一帧，请切换到对应图像或重新匹配。");
        bool verified = FramePairingVerified && CorrectionFrameId.HasValue && CorrectionFrameId == previewFrame;
        return new(State, Source, verified ? "位置修正有效，输入及预览帧配对已验证。" : "位置修正有效；预览图像帧关系未验证（兼容显示）。",
            _matrix, verified, CorrectionFrameId);
    }
    public AffineMatrix? Matrix => Copy(_matrix);
    public string Diagnostic => $"{State}; source={Source}; matrix={_matrix?.ToString() ?? "none"}; {Message}";
    private static AffineMatrix? Copy(AffineMatrix? m) => m == null ? null : new(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY);

    /// <summary>With a query provider, always resolve live data and never fall back to an editor's cached CurrentValue.</summary>
    public static PositionCorrectionResolution Capture(ToolParameters p, IDataSourceQueryService? query = null, bool requireConformal = true)
    {
        // Some plugins hide inherited correction properties. Read the concrete contract used by that tool.
        if (p.GetType().GetProperty("IsPositionCorrectionEnabled")?.GetValue(p) is not true
            || p is IBindingInputActivityProvider activity && !activity.IsBindingInputActive("PositionCorrectionBindingSource"))
            return new(PositionCorrectionState.Disabled, "", "位置修正未启用。");
        var reference = ToolExecutionPreparation.ReadReference(p, "PositionCorrectionBindingSource");
        var node = reference?.SourceNodeId;
        var field = reference?.PropertyName;
        if (string.IsNullOrWhiteSpace(node) || string.IsNullOrWhiteSpace(field))
            return new(PositionCorrectionState.Unbound, "", "请选择位置修正来源。");
        var path = node + "." + field;
        object? value;
        bool frameVerified = false;
        Guid? expectedFrame = null;
        try
        {
            ToolResults? sourceResult = query?.GetNodeContext(node)?.Result;
            if (query == null && p.NodeResults != null) p.NodeResults.TryGetValue(node, out sourceResult);
            if (sourceResult is { IsSuccess: false })
                return new(PositionCorrectionState.WaitingForSource, path, "位置修正来源本次执行未成功。");
            // Execution reads its result snapshot, never an editor's preview value. An explicit
            // snapshot without this source must not silently reuse a previous run's matrix.
            value = query != null
                ? query.GetPropertyValue(node, field)
                : sourceResult != null
                    ? OutputVariableResolver.Resolve(sourceResult, field, node)
                    : p.NodeResults == null ? (p.GetType().GetProperty("PositionCorrectionBindingSource")?.GetValue(p) as AvailableDataSource)?.CurrentValue : null;
            expectedFrame = ImageFrameProvenance.GetInputFrame(sourceResult);
            if (expectedFrame.HasValue)
            {
                var input = ToolExecutionPreparation.ReadReference(p, "ImageSource");
                var image = query != null && input != null
                    ? query.GetPropertyValue(input.SourceNodeId, input.PropertyName) as SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage
                    : p.ResolvedInputImage;
                var currentFrame = ImageFrameProvenance.CaptureInput(image);
                if (image == null)
                    return new(PositionCorrectionState.WaitingForSource, path, "当前输入图像尚不可用，等待图像与位置修正配对。");
                if (currentFrame.HasValue && currentFrame != expectedFrame)
                    return new(PositionCorrectionState.ImageFrameMismatch, path, "位置修正结果不属于当前输入图像，请先对当前图像重新执行匹配。");
                frameVerified = currentFrame == expectedFrame;
            }
            if (query != null && sourceResult != null && !ReferenceEquals(sourceResult, query.GetNodeContext(node)?.Result))
                return new(PositionCorrectionState.WaitingForSource, path, "位置修正来源正在更新，请等待当前图像的完整匹配结果。");
        }
        catch (Exception e) { return new(PositionCorrectionState.WaitingForSource, path, "位置修正来源暂不可用：" + e.Message); }
        if (value == null) return new(PositionCorrectionState.WaitingForSource, path, "位置修正来源尚无有效结果。");
        if (value is AffineMatrix[] { Length: 0 }) return new(PositionCorrectionState.EmptyMatch, path, "位置修正匹配结果为空。");
        var matrix = value is AffineMatrix scalar ? scalar : value is AffineMatrix[] { Length: > 0 } array ? array[0] : null;
        if (matrix == null) return new(PositionCorrectionState.InvalidMatrix, path, "位置修正来源不是有效矩阵。");
        foreach (var v in matrix.ToArray())
            if (!double.IsFinite(v)) return new(PositionCorrectionState.InvalidMatrix, path, "位置修正矩阵包含非有限数值。");
        var det = matrix.M11 * matrix.M22 - matrix.M12 * matrix.M21;
        if (!double.IsFinite(det) || Math.Abs(det) < 1e-10)
            return new(PositionCorrectionState.InvalidMatrix, path, "位置修正矩阵不可逆。");
        if (requireConformal && !matrix.IsConformal())
            return new(PositionCorrectionState.UnsupportedTransform, path, "当前测量引导仅支持平移、旋转和等比缩放，不支持剪切、镜像或非等比缩放。");
        return new(PositionCorrectionState.Valid, path, frameVerified ? "位置修正有效，图像帧配对已验证。" : "位置修正有效；图像帧关系未验证（兼容来源）。", matrix, frameVerified, expectedFrame);
    }

}
