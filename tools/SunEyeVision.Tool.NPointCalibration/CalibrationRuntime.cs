using System.Security.Cryptography;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Models.Calibration;

namespace SunEyeVision.Tool.NPointCalibration;

internal static class CalibrationRuntime
{
    public static PlanarCalibrationSample Native(this NPointSample s) => new(s.Id,
        (PlanarSampleKind)s.Kind, s.ImageX, s.ImageY, s.AxisX, s.AxisY, s.AxisAngle);
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,
        new JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals })));
    public static CalibrationAcquisitionEvidence RequireEvidence(ToolExecutionRequest? request, int delayMs)
    {
        var e = request?.CalibrationEvidence ?? throw new InvalidOperationException(
            "上游尚未提供同一步到位/新帧/轴心XYR证据，触发采样不可用；请使用手动点表或接入采样上下文。");
        request!.CancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(e.SessionId) || e.StepId <= 0 || !e.InPosition || e.FrameId <= 0 ||
            string.IsNullOrWhiteSpace(e.CameraId) || e.ImageWidth <= 0 || e.ImageHeight <= 0 ||
            string.IsNullOrWhiteSpace(e.ImageDomain) || string.IsNullOrWhiteSpace(e.WorkPlaneId) ||
            e.FrameAt == default || e.AxisReadAt == default || delayMs <= 0 ||
            new[]{e.AxisX,e.AxisY,e.AxisAngle}.Any(v=>!double.IsFinite(v)) ||
            Math.Abs((e.FrameAt-e.AxisReadAt).TotalMilliseconds) > delayMs)
            throw new InvalidOperationException("上游采样证据缺失、机构未到位或图像/XYR超出配对时间窗。");
        if (request.CameraFrame is { } frame && (frame.FrameId != e.FrameId || frame.Timestamp != e.FrameAt))
            throw new InvalidOperationException("本轮相机帧与上游采样证据不一致。");
        return e;
    }
    public static void Attach(NPointSample s, CalibrationAcquisitionEvidence e)
    {
        if(Math.Abs(s.AxisX-e.AxisX)>1e-8 || Math.Abs(s.AxisY-e.AxisY)>1e-8 ||
            Math.Abs(SunEyeVision.HalAdapter.HalconPlanarCalibration.AngleDifference(s.AxisAngle,e.AxisAngle))>1e-8)
            throw new InvalidOperationException("绑定轴心XYR与本轮回读快照不一致，拒绝混用不同采样步。");
        s.FrameId = e.FrameId; s.FrameCapturedAt = e.FrameAt; s.AxisReadAt = e.AxisReadAt;
        s.SessionId = e.SessionId; s.StepId = e.StepId; s.CameraId = e.CameraId;
        s.WorkPlaneId = e.WorkPlaneId;
    }
}
