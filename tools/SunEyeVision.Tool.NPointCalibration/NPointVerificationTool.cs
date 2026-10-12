using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.NPointCalibration;

/// <summary>Thin workflow node: validate new observations against an existing candidate.</summary>
[Tool("NPointVerification", "N点标定验证",
    Description = "加载候选标定文件，用独立平移和旋转采样验证并发布",
    Icon = "verify", Order = 20, Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Calibration, Version = "1.0.0", HasDebugWindow = true)]
public sealed class NPointVerificationTool : IToolPlugin<NPointVerificationParameters, NPointVerificationResults>,
    IAsyncToolPlugin, IToolEditorFactory
{
    public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        => new Views.NPointVerificationDebugControl();
    public NPointVerificationResults Run(NPointVerificationParameters parameters) => Execute(parameters);

    public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        => ValueTask.FromResult<ToolResults>(Execute((NPointVerificationParameters)request.Parameters));

    private static NPointVerificationResults Execute(NPointVerificationParameters p)
    {
        var result = new NPointVerificationResults
        {
            ToolId = "NPointVerification", ToolName = "N点标定验证",
            ExpectedRevision = p.SampleRevision,
        };
        try
        {
            var (candidate, hash) = NPointCalibrationFile.ReadCandidate(p.CandidateFilePath);
            result.CandidateSha256 = hash;
            if (p.ValidationSamples.Count > 0 && p.CandidateSha256 != hash)
                throw new InvalidOperationException("候选文件已变化，旧验证点不可沿用；请清空验证点重新采集。");
            if (!string.Equals(p.SourceDeviceId, candidate.SourceDeviceId, StringComparison.Ordinal)
                || p.ImageWidth != candidate.ImageWidth || p.ImageHeight != candidate.ImageHeight
                || !string.Equals(p.ImageDomain, candidate.ImageDomain, StringComparison.Ordinal))
                throw new InvalidOperationException("验证图像的相机/尺寸/图像域与候选标定不一致。");
            var samples = p.ValidationSamples.Select(s => s.Copy()).ToList();
            if (p.CaptureSample && p.SampleEventId <= 0)
                throw new InvalidOperationException("独立采样必须提供正的事件序号。");
            if (p.CaptureSample && p.SampleEventId > p.LastSampleEventId)
            {
                if (p.FrameId <= 0 || !DateTimeOffset.TryParse(p.FrameTimestampText,
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var frameAt)
                    || p.AxisReadUnixMs <= 0)
                    throw new InvalidOperationException("独立验证缺少新帧号/帧时间/机构读数时间。");
                var axisAt = DateTimeOffset.FromUnixTimeMilliseconds(p.AxisReadUnixMs);
                if (p.MaxPairingDelayMs <= 0 || Math.Abs((frameAt - axisAt).TotalMilliseconds) > p.MaxPairingDelayMs)
                    throw new InvalidOperationException("独立验证图像与机构回读超出配对时间窗。");
                var sample = new NPointSample
                {
                    Id = samples.Count, Kind = p.SampleKind,
                    ImageX = p.ImageX, ImageY = p.ImageY,
                    AxisX = p.AxisX, AxisY = p.AxisY, AxisAngle = p.AxisAngle,
                    FrameId = p.FrameId, FrameCapturedAt = frameAt, AxisReadAt = axisAt,
                    Source = "VerificationWorkflow",
                };
                // Check finite values and reject duplicate training observations before committing.
                candidate.Evaluate(sample);
                if (samples.Any(s => s.FrameId == sample.FrameId)
                    || candidate.Samples.Any(s => s.FrameId.HasValue && s.FrameId == sample.FrameId
                    || s.ImageX == sample.ImageX && s.ImageY == sample.ImageY
                        && s.AxisX == sample.AxisX && s.AxisY == sample.AxisY
                        && s.AxisAngle == sample.AxisAngle))
                    throw new InvalidOperationException("验证样本与训练样本重复。");
                result.CollectedSample = sample;
                result.EventId = p.SampleEventId;
                result.NeedsCommit = true;
                samples.Add(sample);
            }
            result.CollectedCount = samples.Count;
            if (samples.Any(s => s.Kind == NPointSampleKind.Translation)
                && samples.Any(s => s.Kind == NPointSampleKind.Rotation))
            {
                var residuals = candidate.Validate(samples, p.MaximumValidationErrorMm);
                result.MaximumErrorMm = residuals.Max(r => r.ErrorMm);
                result.Residuals = residuals.ToList();
                result.ValidationPassed = true;
                result.Message = "独立验证通过；可发布此候选文件。";
                if (p.LastPublishedCandidateSha256 == hash && File.Exists(p.ActiveFilePath))
                {
                    try
                    {
                        var active = NPointCalibrationFile.ReadVerified(p.ActiveFilePath,
                            p.SourceDeviceId, p.ImageWidth, p.ImageHeight, p.ImageDomain);
                        result.Published = active.CandidateSha256 == hash;
                    }
                    catch (Exception) { result.Published = false; }
                }
                if (p.PublishOnPass && !result.Published)
                {
                    result.PublishAfterCommit = true;
                    result.NeedsCommit = true;
                }
            }
            else result.Message = "等待独立平移和旋转验证点。";
            result.SetSuccess();
        }
        catch (Exception ex) { result.SetError($"标定验证失败：{ex.Message}", ex); }
        return result;
    }
}

[JsonDerivedType(typeof(NPointVerificationParameters), "NPointVerification")]
public class NPointVerificationParameters : ToolParameters, IWorkflowResultCommitter
{
    private readonly object _sync = new();
    public string CandidateFilePath { get; set; } = string.Empty;
    public string ActiveFilePath { get; set; } = string.Empty;
    public string SourceDeviceId { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ImageDomain { get; set; } = "Raw";
    public string CandidateSha256 { get; set; } = string.Empty;
    public string LastPublishedCandidateSha256 { get; set; } = string.Empty;
    public double MaximumValidationErrorMm { get; set; }
    public bool CaptureSample { get; set; }
    public long SampleEventId { get; set; }
    public long LastSampleEventId { get; set; }
    public NPointSampleKind SampleKind { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisAngle { get; set; }
    public long FrameId { get; set; }
    public string FrameTimestampText { get; set; } = string.Empty;
    public long AxisReadUnixMs { get; set; }
    public int MaxPairingDelayMs { get; set; } = 100;
    public bool PublishOnPass { get; set; }
    public List<NPointSample> ValidationSamples { get; set; } = new();
    public int SampleRevision { get; set; }

    public override void CopyFrom(ToolParameters other)
    {
        base.CopyFrom(other);
        if (other is NPointVerificationParameters source)
        {
            lock (source._sync)
                ValidationSamples = source.ValidationSamples.Select(s => s.Copy()).ToList();
        }
    }

    public bool TryCommit(ToolResults output, out string? error)
    {
        error = null;
        if (output is not NPointVerificationResults result || !result.NeedsCommit)
            return true;
        lock (_sync)
        {
            if (SampleRevision != result.ExpectedRevision
                || result.CollectedSample != null && result.EventId <= LastSampleEventId)
            {
                error = "验证点集已更新；拒绝重复或过期采样。";
                return false;
            }
            if (result.PublishAfterCommit)
            {
                try
                {
                    var samples = result.CollectedSample == null ? ValidationSamples.ToArray()
                        : ValidationSamples.Append(result.CollectedSample).ToArray();
                    NPointCalibrationFile.Publish(CandidateFilePath, ActiveFilePath,
                        result.CandidateSha256, samples, MaximumValidationErrorMm);
                    result.Published = true;
                    LastPublishedCandidateSha256 = result.CandidateSha256;
                }
                catch (Exception ex) { error = $"发布失败，验证点尚未提交：{ex.Message}"; return false; }
            }
            if (result.CollectedSample != null)
            {
                ValidationSamples.Add(result.CollectedSample.Copy());
                LastSampleEventId = result.EventId;
                SampleRevision++;
            }
            CandidateSha256 = result.CandidateSha256;
        }
        return true;
    }
}

public sealed class NPointVerificationResults : ToolResults
{
    public bool ValidationPassed { get; set; }
    public bool Published { get; set; }
    public string Message { get; set; } = string.Empty;
    public string CandidateSha256 { get; set; } = string.Empty;
    public int CollectedCount { get; set; }
    public double MaximumErrorMm { get; set; }
    public List<NPointSampleResidual> Residuals { get; set; } = new();
    [JsonIgnore] public NPointSample? CollectedSample { get; set; }
    [JsonIgnore] public bool NeedsCommit { get; set; }
    [JsonIgnore] public bool PublishAfterCommit { get; set; }
    [JsonIgnore] public long EventId { get; set; }
    [JsonIgnore] public int ExpectedRevision { get; set; }

    public override List<PropertyMapEntry> GetPropertyMap() =>
    [
        .. base.GetPropertyMap(),
        new("ValidationPassed", "独立验证通过", "验证"),
        new("Published", "已发布", "验证"),
        new("MaximumErrorMm", "独立验证最大误差", "验证"),
        new("Message", "验证状态", "验证"),
    ];
}
