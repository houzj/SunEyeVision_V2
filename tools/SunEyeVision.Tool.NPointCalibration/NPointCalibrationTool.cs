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
using SunEyeVision.HalAdapter;

namespace SunEyeVision.Tool.NPointCalibration;

[Tool("NPointCalibration", "N点标定",
    Description = "采集图像/机构对应点并求解平面标定",
    Icon = "axes", Order = 10, Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Calibration, Version = "1.0.0", HasDebugWindow = true)]
public sealed class NPointCalibrationTool : IToolPlugin<NPointCalibrationParameters, NPointCalibrationResults>,
    IAsyncToolPlugin, IToolEditorFactory
{
    public NPointCalibrationResults Run(NPointCalibrationParameters parameters)
        => Execute(parameters, new ToolExecutionRequest(parameters) { CancellationToken=parameters.Context?.CancellationToken ?? default });

    public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        => ValueTask.FromResult<ToolResults>(Execute((NPointCalibrationParameters)request.Parameters, request));

    private static NPointCalibrationResults Execute(NPointCalibrationParameters parameters, ToolExecutionRequest? request)
    {
        var result = new NPointCalibrationResults
        {
            ToolName = "N点标定", ToolId = "NPointCalibration",
            ResourcePaths = request?.ResourcePaths ?? parameters.Context?.ResourcePaths,
            RequiredTranslationSamples = parameters.TranslationCount,
            RequiredRotationSamples = parameters.RotationCount,
            ExpectedRevision = parameters.SampleRevision,
            InputFingerprint = parameters.Fingerprint(),
            Cancellation = request?.CancellationToken ?? default,
        };
        try
        {
            request?.CancellationToken.ThrowIfCancellationRequested();
            if (parameters.Samples.Count > 0 && parameters.SamplesContractVersion != 3)
                throw new InvalidOperationException("旧点表不做兼容，请清空后重新采集或按新契约重新录入。");
            if (parameters.TranslationCount < 9 || parameters.RotationCount < 3 || parameters.TranslationCount>100 || parameters.RotationCount>100)
                throw new InvalidOperationException("原生9+3标定至少需要9个平移点和3个旋转点。");
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
            if (active)
            {
                if (parameters.Samples.Count >= parameters.TranslationCount + parameters.RotationCount)
                    throw new InvalidOperationException("标定点已采满；请检查点表或开始新一轮标定。");
                var evidence = CalibrationRuntime.RequireEvidence(request, parameters.MaxPairingDelayMs);
                if (parameters.Samples.Any(s => s.Source == "WorkflowFrame+Encoder" &&
                    (s.CameraId != evidence.CameraId || s.WorkPlaneId != evidence.WorkPlaneId || s.SessionId != evidence.SessionId ||
                        parameters.ImageWidth!=evidence.ImageWidth || parameters.ImageHeight!=evidence.ImageHeight || parameters.ImageDomain!=evidence.ImageDomain)))
                    throw new InvalidOperationException("采样会话、相机或工作平面已变化，请开始新的标定点集。");
                var sample = Capture(parameters, evidence.FrameId, evidence.FrameAt, evidence.AxisReadAt);
                CalibrationRuntime.Attach(sample, evidence);
                if (parameters.Samples.Any(s => s.SessionId == evidence.SessionId &&
                    (s.StepId == evidence.StepId || s.FrameId == evidence.FrameId && s.CameraId == evidence.CameraId)))
                    throw new InvalidOperationException("同一采样会话重复运动步或图像帧，拒绝重复采点。");
                result.CollectedSample = sample;
                result.NeedsCommit = true;
                result.SourceDeviceId = evidence.CameraId; result.ImageWidth = evidence.ImageWidth;
                result.ImageHeight = evidence.ImageHeight; result.ImageDomain = evidence.ImageDomain;
                result.WorkPlaneId = evidence.WorkPlaneId;
                samples.Add(sample);
            }

            result.TranslationSamples = samples.Count(s => s.Kind == NPointSampleKind.Translation);
            result.RotationSamples = samples.Count(s => s.Kind == NPointSampleKind.Rotation);

            if (result.TranslationSamples >= parameters.TranslationCount
                && result.RotationSamples >= parameters.RotationCount)
            {
                var fitted = HalconPlanarCalibration.Fit(samples.Select(s => s.Native()).ToArray(),
                    parameters.NativeSettings(), request?.CancellationToken ?? default);
                var residuals = fitted.Residuals.Select(r => new NPointSampleResidual(r.Id,
                    (NPointSampleKind)r.Kind, r.ErrorX, r.ErrorY, r.ErrorMm)).ToList();
                result.HomMat2D = fitted.Matrix;
                result.AuxiliaryMatrix = fitted.AuxiliaryMatrix;
                result.MeanError = residuals.Average(r => r.ErrorMm);
                result.MaxError = residuals.Max(r => r.ErrorMm);
                double Rms(IEnumerable<NPointSampleResidual> values) => Math.Sqrt(values.Average(r => r.ErrorMm*r.ErrorMm));
                result.RmsError = Rms(residuals);
                result.TranslationRmsError = Rms(residuals.Where(r => r.Kind == NPointSampleKind.Translation));
                result.RotationRmsError = Rms(residuals.Where(r => r.Kind == NPointSampleKind.Rotation));
                result.AxisToMarkX = fitted.OffsetX;
                result.AxisToMarkY = fitted.OffsetY;
                if (result.CollectedSample == null)
                {
                    result.SourceDeviceId = parameters.SourceDeviceId; result.ImageWidth = parameters.ImageWidth;
                    result.ImageHeight = parameters.ImageHeight; result.ImageDomain = parameters.ImageDomain;
                    result.WorkPlaneId = parameters.WorkPlaneId;
                }
                result.ReferenceRAngle = parameters.ReferenceRAngle;
                result.RotationSign = parameters.RotationSign;
                result.RotationSpanDeg = fitted.RotationSpan;
                result.RepeatErrorMm = fitted.RepeatErrorMm;
                result.Residuals = residuals;
                result.CalibrationReady = true;
                if (!double.IsFinite(parameters.MaximumTrainingErrorMm) || parameters.MaximumTrainingErrorMm <= 0)
                    throw new InvalidOperationException("请设置现场最大允许训练误差(mm)。");
                if (result.MaxError > parameters.MaximumTrainingErrorMm)
                    throw new InvalidOperationException($"点 {residuals.MaxBy(r => r.ErrorMm)!.Id + 1} 最大误差 {result.MaxError:F6} mm 超限。");
                result.TrainingPassed = true;
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
                        HalconPlanarCalibration.Transform(result.HomMat2D,
                            result.CurrentImageX, result.CurrentImageY);
                    result.HasConvertedPoint = true;
                }
            }
            result.SetSuccess();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            result.SetError($"N点标定失败：{ex.Message}", ex);
        }
        return result;
    }

    private static NPointSample Capture(NPointCalibrationParameters p, long? frameId,
        DateTimeOffset? frameCapturedAt, DateTimeOffset? axisReadAt)
    {
        int index = Enumerable.Range(0, p.TranslationCount + p.RotationCount)
            .First(i => p.Samples.All(sample => sample.Id != i));
        double ix=p.ImageX, iy=p.ImageY, ax=p.AxisX, ay=p.AxisY;
        if (p.InputMode == NPointInputMode.Point)
        {
            if(p.ImagePoint.Length!=1 || p.AxisPoint.Length!=1)
                throw new InvalidOperationException("图像点与机构轴心绑定必须各有一个点。");
            ix=p.ImagePoint[0].X;iy=p.ImagePoint[0].Y;ax=p.AxisPoint[0].X;ay=p.AxisPoint[0].Y;
        }
        if (new[]{ix,iy,ax,ay,p.AxisAngle}.Any(v=>!double.IsFinite(v)))
            throw new InvalidOperationException("标定点含非有限坐标。");
        return new NPointSample {Id=index,Kind=index<p.TranslationCount?NPointSampleKind.Translation:NPointSampleKind.Rotation,
            ImageX=ix,ImageY=iy,AxisX=ax,AxisY=ay,AxisAngle=p.AxisAngle,FrameId=frameId,
            FrameCapturedAt=frameCapturedAt,AxisReadAt=axisReadAt,Source="WorkflowFrame+Encoder"};
    }

    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        => new Views.NPointCalibrationDebugControl();
}
