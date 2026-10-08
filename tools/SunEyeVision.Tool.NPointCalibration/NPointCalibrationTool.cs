using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.NPointCalibration;

[Tool("NPointCalibration", "N点标定",
    Description = "采集图像/机构对应点并求解平面标定",
    Icon = "◎", Category = "标定", Version = "1.0.0", HasDebugWindow = true)]
public sealed class NPointCalibrationTool : IToolPlugin<NPointCalibrationParameters, NPointCalibrationResults>,
    IAsyncToolPlugin, IToolEditorFactory
{
    public NPointCalibrationResults Run(NPointCalibrationParameters parameters)
        => Execute(parameters, null);

    public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        => ValueTask.FromResult<ToolResults>(Execute((NPointCalibrationParameters)request.Parameters, request));

    private static NPointCalibrationResults Execute(NPointCalibrationParameters parameters, ToolExecutionRequest? request)
    {
        var result = new NPointCalibrationResults
        {
            ToolName = "N点标定", ToolId = "NPointCalibration",
            RequiredTranslationSamples = parameters.TranslationCount,
            RequiredRotationSamples = parameters.RotationCount,
            ExpectedRevision = parameters.SampleRevision,
        };
        try
        {
            if (parameters.TranslationCount < 4 || parameters.RotationCount < 3)
                throw new InvalidOperationException("联合标定至少需要 4 个平移点和 3 个旋转点。");
            if (parameters.CameraMode != NPointCameraMode.FixedAbove)
                throw new NotSupportedException("当前仅支持固定相机上相机位；其他相机模式不能复用同一变换方向。");
            if (parameters.TransformModel != NPointTransformModel.Affine)
                throw new NotSupportedException("9+3 联合模型只支持 6 自由度仿射变换。");

            var samples = parameters.Samples.Where(s => s.Enabled).Select(s => s.Copy()).ToList();
            bool active = parameters.AcquisitionMode == NPointAcquisitionMode.Trigger
                && (parameters.TeachSignal || (parameters.TeachEnabled
                    && !string.IsNullOrWhiteSpace(parameters.TeachTriggerText)
                    && string.Equals(parameters.ExternalInputText, parameters.TeachTriggerText,
                        StringComparison.Ordinal)));
            result.TeachSignalActive = active;
            result.TeachEventId = parameters.TeachEventId;
            bool newTeachEvent = active && (parameters.TeachEventId > 0
                ? parameters.TeachEventId > parameters.LastTeachEventId
                : !parameters.LastTeachSignal);
            result.NeedsCommit = parameters.AcquisitionMode == NPointAcquisitionMode.Trigger
                && (active != parameters.LastTeachSignal || newTeachEvent);

            if (newTeachEvent)
            {
                if (parameters.Samples.Count >= parameters.TranslationCount + parameters.RotationCount)
                    throw new InvalidOperationException("标定点已采满；请检查点表或开始新一轮标定。");
                var sample = Capture(parameters, request?.CameraFrame?.FrameId,
                    request?.CameraFrame?.Timestamp);
                result.CollectedSample = sample;
                samples.Add(sample);
            }

            result.TranslationSamples = samples.Count(s => s.Kind == NPointSampleKind.Translation);
            result.RotationSamples = samples.Count(s => s.Kind == NPointSampleKind.Rotation);

            if (result.TranslationSamples >= parameters.TranslationCount
                && result.RotationSamples >= parameters.RotationCount)
            {
                var fitted = NPointCalibrationSolver.Fit(parameters, samples);
                result.HomMat2D = fitted.Matrix;
                result.MeanError = fitted.MeanError;
                result.MaxError = fitted.MaxError;
                result.RmsError = fitted.RmsError;
                result.TranslationRmsError = fitted.TranslationRmsError;
                result.RotationRmsError = fitted.RotationRmsError;
                result.AxisToMarkX = fitted.AxisToMarkX;
                result.AxisToMarkY = fitted.AxisToMarkY;
                result.SourceDeviceId = parameters.SourceDeviceId;
                result.ImageWidth = parameters.ImageWidth;
                result.ImageHeight = parameters.ImageHeight;
                result.ImageDomain = parameters.ImageDomain;
                result.ReferenceRAngle = parameters.ReferenceRAngle;
                result.RotationSign = parameters.RotationSign;
                result.WeightFunction = parameters.WeightFunction;
                result.WeightCoefficient = parameters.WeightCoefficient;
                result.RobustScaleMm = fitted.RobustScaleMm;
                result.RobustThresholdMm = fitted.RobustThresholdMm;
                result.Residuals = fitted.Residuals;
                result.CalibrationReady = true;
                (double X, double Y)? livePoint = result.CollectedSample != null
                    ? (result.CollectedSample.ImageX, result.CollectedSample.ImageY)
                    : parameters.InputMode == NPointInputMode.Point && parameters.ImagePoint.Length == 1
                        ? (parameters.ImagePoint[0].X, parameters.ImagePoint[0].Y)
                        : parameters.InputMode == NPointInputMode.Coordinates
                            ? (parameters.ImageX, parameters.ImageY)
                            : ((double X, double Y)?)null;
                if (livePoint.HasValue)
                {
                    result.CurrentImageX = livePoint.Value.X;
                    result.CurrentImageY = livePoint.Value.Y;
                    (result.MarkMachineX, result.MarkMachineY) =
                        NPointCalibrationSolver.TransformPoint(result.HomMat2D,
                            result.CurrentImageX, result.CurrentImageY);
                    result.HasConvertedPoint = true;
                }
            }
            result.SetSuccess();
        }
        catch (Exception ex)
        {
            result.SetError($"N点标定失败：{ex.Message}", ex);
        }
        return result;
    }

    private static NPointSample Capture(NPointCalibrationParameters p, long? frameId,
        DateTimeOffset? frameCapturedAt)
    {
        int index = Enumerable.Range(0, p.TranslationCount + p.RotationCount)
            .First(i => p.Samples.All(sample => sample.Id != i));
        double ix, iy, ax, ay;
        if (p.InputMode == NPointInputMode.Point)
        {
            if (p.ImagePoint.Length != 1)
                throw new InvalidOperationException("图像点绑定必须恰好输出一个点。");
            ix = p.ImagePoint[0].X;
            iy = p.ImagePoint[0].Y;
            if (p.AxisPoint.Length != 1)
                throw new InvalidOperationException("R 轴轴心 XY 绑定必须恰好输出一个点。");
            ax = p.AxisPoint[0].X;
            ay = p.AxisPoint[0].Y;
        }
        else
        {
            ix = p.ImageX; iy = p.ImageY;
            (ax, ay) = (p.AxisX, p.AxisY);
        }
        if (!double.IsFinite(ix) || !double.IsFinite(iy) || !double.IsFinite(ax) || !double.IsFinite(ay)
            || !double.IsFinite(p.AxisAngle))
            throw new InvalidOperationException("标定点存在非有限数值。");
        if (!frameCapturedAt.HasValue && !string.IsNullOrWhiteSpace(p.FrameTimestampText))
        {
            if (!DateTimeOffset.TryParse(p.FrameTimestampText, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
                throw new InvalidOperationException("图像帧时间不是有效的 ISO 8601 时间。");
            frameCapturedAt = parsed;
        }
        if (!frameCapturedAt.HasValue)
            throw new InvalidOperationException("触发采样缺少图像帧时间；请绑定图像源采集时间。");
        if (!frameId.HasValue && long.TryParse(p.FrameIdText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsedFrameId))
            frameId = parsedFrameId;
        if (!frameId.HasValue)
            throw new InvalidOperationException("触发采样缺少图像帧号；请绑定图像源设备帧号。");
        DateTimeOffset? axisReadAt = null;
        if (p.AxisReadUnixMs > 0)
            axisReadAt = DateTimeOffset.FromUnixTimeMilliseconds(p.AxisReadUnixMs);
        if (!axisReadAt.HasValue)
            throw new InvalidOperationException("触发采样缺少编码器读数时间，不能证明 XYR 与图像帧同次。");
        if (p.MaxPairingDelayMs <= 0 || Math.Abs((frameCapturedAt.Value - axisReadAt.Value).TotalMilliseconds) > p.MaxPairingDelayMs)
            throw new InvalidOperationException("图像帧与编码器读数超出配对时间窗。");
        return new NPointSample
        {
            Id = index, Kind = index < p.TranslationCount
                ? NPointSampleKind.Translation : NPointSampleKind.Rotation,
            ImageX = ix, ImageY = iy, AxisX = ax, AxisY = ay, AxisAngle = p.AxisAngle,
            FrameId = frameId,
            FrameCapturedAt = frameCapturedAt,
            AxisReadAt = axisReadAt,
            Source = "WorkflowFrame+Encoder",
        };
    }

    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        => new Views.NPointCalibrationDebugControl();
}
