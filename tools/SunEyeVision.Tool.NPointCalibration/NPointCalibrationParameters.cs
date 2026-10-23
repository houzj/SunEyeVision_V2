using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Calibration;

namespace SunEyeVision.Tool.NPointCalibration;

public enum NPointAcquisitionMode { Trigger, Manual }
public enum NPointInputMode { Point, Coordinates }
public enum NPointCameraMode { FixedAbove, FixedBelow, Moving }
public enum NPointTransformModel { Similarity, Affine, Projective }
public enum NPointSampleKind { Translation, Rotation }

public sealed class NPointSample
{
    public int Id { get; set; }
    [JsonIgnore] public int DisplayId => Id+1;
    public NPointSampleKind Kind { get; set; }
    [JsonIgnore]
    public string KindText => Kind == NPointSampleKind.Translation ? "平移" : "旋转";
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    /// <summary>R-axis center encoder position in absolute machine coordinates.</summary>
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    /// <summary>R-axis encoder angle in degrees, never the observed Mark angle.</summary>
    public double AxisAngle { get; set; }
    public bool Enabled { get; set; } = true;
    public long? FrameId { get; set; }
    public DateTimeOffset? FrameCapturedAt { get; set; }
    public DateTimeOffset? AxisReadAt { get; set; }
    public string Source { get; set; } = "Manual";
    public string SessionId { get; set; } = string.Empty;
    public long StepId { get; set; }
    public string CameraId { get; set; } = string.Empty;
    public string WorkPlaneId { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore] public string StatusText => Source == "Placeholder" ? "待采集" : !Enabled ? "停用" : Source.StartsWith("Manual") ? "手动" : "触发";
    [JsonIgnore] public string ImageXDisplay => Display(ImageX);
    [JsonIgnore] public string ImageYDisplay => Display(ImageY);
    [JsonIgnore] public string AxisXDisplay => Display(AxisX);
    [JsonIgnore] public string AxisYDisplay => Display(AxisY);
    [JsonIgnore] public string AxisAngleDisplay => Display(AxisAngle);
    private string Display(double value) => Source == "Placeholder" ? string.Empty : value.ToString("F3");

    public NPointSample Copy() => (NPointSample)MemberwiseClone();
}

[JsonDerivedType(typeof(NPointCalibrationParameters), "NPointCalibration")]
public class NPointCalibrationParameters : ToolParameters, IWorkflowResultCommitter, IJsonOnDeserializing
{
    public int SamplesContractVersion { get; set; } = 3;
    void IJsonOnDeserializing.OnDeserializing() => SamplesContractVersion = 0;
    private readonly object _sync = new();
    private NPointAcquisitionMode _acquisitionMode = NPointAcquisitionMode.Trigger;
    public NPointAcquisitionMode AcquisitionMode
    {
        get => _acquisitionMode;
        set => SetProperty(ref _acquisitionMode, value);
    }
    private NPointInputMode _inputMode = NPointInputMode.Point;
    public NPointInputMode InputMode
    {
        get => _inputMode;
        set => SetProperty(ref _inputMode, value);
    }
    public Point2d[] ImagePoint { get; set; } = Array.Empty<Point2d>();
    public Point2d[] AxisPoint { get; set; } = Array.Empty<Point2d>();
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double AxisX { get; set; }
    public double AxisY { get; set; }
    public double AxisAngle { get; set; }
    public string SourceDeviceId { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ImageDomain { get; set; } = "Raw";
    public int MaxPairingDelayMs { get; set; } = 100;
    public string WorkPlaneId { get; set; } = string.Empty;
    public int ReferenceSampleId { get; set; } = 4;
    [JsonIgnore] public int ReferencePointNumber { get => ReferenceSampleId+1; set => ReferenceSampleId=value-1; }
    public double RepeatToleranceMm { get; set; } = 0.01;
    public double ReferenceRAngle { get; set; }
    public int RotationSign { get; set; } = 1;
    public double TranslationAngleToleranceDeg { get; set; } = 0.1;
    public double RotationAxisToleranceMm { get; set; } = 0.01;
    public double MinimumRotationSpanDeg { get; set; } = 20;

    /// <summary>Upstream signal; a false-to-true transition captures one pair.</summary>
    public bool TeachSignal { get; set; }
    private bool _teachEnabled;
    public bool TeachEnabled
    {
        get => _teachEnabled;
        set => SetProperty(ref _teachEnabled, value);
    }
    public string ExternalInputText { get; set; } = string.Empty;
    public string TeachTriggerText { get; set; } = "TEACH";
    private int _translationCount = 9;
    public int TranslationCount
    {
        get => _translationCount;
        set => SetProperty(ref _translationCount, value);
    }
    private int _rotationCount = 3;
    public int RotationCount
    {
        get => _rotationCount;
        set => SetProperty(ref _rotationCount, value);
    }
    private string _calibrationFilePath = string.Empty;
    public string CalibrationFilePath
    {
        get => _calibrationFilePath;
        set => SetProperty(ref _calibrationFilePath, value);
    }
    public bool UpdateFileAfterSolve { get; set; }
    public NPointCameraMode CameraMode { get; set; } = NPointCameraMode.FixedAbove;
    public NPointTransformModel TransformModel { get; set; } = NPointTransformModel.Affine;
    /// <summary>Site-specific maximum training-point residual; required before writing a candidate.</summary>
    public double MaximumTrainingErrorMm { get; set; }

    public List<NPointSample> Samples { get; set; } = new();
    public List<NPointSample> ArchivedSamples { get; set; } = new();
    public int SampleRevision { get; set; }
    public int LastWrittenRevision { get; set; } = -1;
    public string LastWrittenFingerprint { get; set; } = string.Empty;
    [JsonIgnore] public NPointCalibrationResults? RuntimeResult { get; private set; }
    public string Fingerprint() => CalibrationRuntime.Hash(new { TranslationCount, RotationCount, ReferenceRAngle,
        RotationSign, TranslationAngleToleranceDeg, RotationAxisToleranceMm, MinimumRotationSpanDeg,
        ReferenceSampleId, RepeatToleranceMm, MaximumTrainingErrorMm, MaxPairingDelayMs, SourceDeviceId, ImageWidth, ImageHeight,
        ImageDomain, WorkPlaneId, CameraMode, TransformModel, CalibrationFilePath, UpdateFileAfterSolve, SampleRevision, SamplesContractVersion, Samples });
    public PlanarCalibrationSettings NativeSettings() => new(TranslationCount, RotationCount, ReferenceRAngle,
        RotationSign, TranslationAngleToleranceDeg, RotationAxisToleranceMm, MinimumRotationSpanDeg,
        ReferenceSampleId, RepeatToleranceMm);

    public override void CopyFrom(ToolParameters other)
    {
        base.CopyFrom(other);
        if (other is NPointCalibrationParameters source)
        {
            lock (source._sync)
            {
                Samples = source.Samples.Select(sample => sample.Copy()).ToList();
                ArchivedSamples = source.ArchivedSamples.Select(sample => sample.Copy()).ToList();
            }
        }
    }

    [JsonIgnore]
    public string ImagePointBindingSource { get; set; } = string.Empty;
    [JsonIgnore]
    public string AxisPointBindingSource { get; set; } = string.Empty;

    public bool TryCommit(ToolResults result, out string? error)
    {
        error = null;
        if (result is not NPointCalibrationResults calibration)
            return true;

        lock (_sync)
        {
            if (!calibration.IsSuccess || calibration.Cancellation.IsCancellationRequested) { error = "失败或已取消结果不可提交。"; return false; }
            if (SampleRevision != calibration.ExpectedRevision || Fingerprint() != calibration.InputFingerprint)
            {
                error = "标定点集在本轮运行中被修改；已拒绝过期采样，请重试。";
                return false;
            }

            if (calibration.CalibrationReady && UpdateFileAfterSolve
                && string.IsNullOrWhiteSpace(CalibrationFilePath))
            {
                error = "已开启候选文件自动更新，但未设置文件路径；本次采样尚未提交。";
                return false;
            }

            if (calibration.NeedsCommit && calibration.CollectedSample != null)
            {
                if (Samples.Count >= TranslationCount + RotationCount)
                {
                    error = "标定点数量已满，请先编辑或清空点集。";
                    return false;
                }
                Samples.Add(calibration.CollectedSample.Copy());
                SourceDeviceId = calibration.SourceDeviceId; ImageWidth = calibration.ImageWidth;
                ImageHeight = calibration.ImageHeight; ImageDomain = calibration.ImageDomain;
                WorkPlaneId = calibration.WorkPlaneId;
            }
            if (calibration.NeedsCommit)
            {
                SampleRevision++;
                if (calibration.CollectedSample != null)
                    OnPropertyChanged(nameof(Samples));
            }
            calibration.AcceptedFingerprint = Fingerprint();
            RuntimeResult = calibration;
            if (calibration.CalibrationReady && UpdateFileAfterSolve
                && LastWrittenFingerprint != calibration.AcceptedFingerprint)
            {
                try
                {
                    WriteCandidateFile(ResolveActivityPath(CalibrationFilePath, calibration.ResourcePaths), calibration, Samples, MaximumTrainingErrorMm,ArchivedSamples);
                    LastWrittenRevision = SampleRevision;
                    LastWrittenFingerprint = calibration.AcceptedFingerprint;
                }
                catch (Exception ex)
                {
                    error = $"标定点已记录，但更新候选文件失败：{ex.Message}";
                    return false;
                }
            }
        }
        return true;
    }

    public static string ResolveActivityPath(string reference, SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver? resolver)
        => resolver?.Resolve(nameof(CalibrationFilePath), reference, SunEyeVision.Plugin.SDK.Execution.Parameters.ResourcePurpose.FileWrite).Address
            ?? (Path.IsPathRooted(reference) ? Path.GetFullPath(reference) : throw new InvalidOperationException("相对标定路径需要方案资源上下文。"));

    public static string CandidatePath(string activityPath)
    {
        string fullPath = Path.GetFullPath(activityPath);
        return Path.Combine(Path.GetDirectoryName(fullPath)!,
            Path.GetFileNameWithoutExtension(fullPath) + ".candidate.json");
    }

    public static void WriteCandidateFile(string activityPath, NPointCalibrationResults result,
        IEnumerable<NPointSample> samples, double maximumTrainingErrorMm, IEnumerable<NPointSample>? archived=null)
    {
        if (!result.CalibrationReady || !result.TrainingPassed || result.HomMat2D.Length != 6)
            throw new InvalidOperationException("原生标定训练未合格，不能生成候选文件。");
        if (!double.IsFinite(maximumTrainingErrorMm) || maximumTrainingErrorMm <= 0
            || !double.IsFinite(result.MaxError) || result.MaxError > maximumTrainingErrorMm)
            throw new InvalidOperationException("未设置有效训练点误差门槛，或训练点最大残差超限，不能生成候选文件。");
        string fullPath = CandidatePath(activityPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tempPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            var document = new
            {
                SchemaVersion = PlanarCalibrationFit.FileVersion,
                AlgorithmId = PlanarCalibrationFit.Algorithm,
                Direction = "ImageColumnRowToMachineMarkXY",
                PhysicalUnit = "mm",
                AxisCoordinates = "R-axis center encoder XY",
                State = "SolvedUnverified",
                CreatedAt = DateTimeOffset.UtcNow,
                Matrix = result.HomMat2D,
                result.ReferenceRAngle,
                result.RotationSign,
                result.AxisToMarkX,
                result.AxisToMarkY,
                result.SourceDeviceId,
                result.ImageWidth,
                result.ImageHeight,
                result.ImageDomain,
                result.WorkPlaneId,
                result.RepeatErrorMm,
                result.RotationSpanDeg,
                MaximumTrainingErrorMm = maximumTrainingErrorMm,
                result.MeanError,
                result.MaxError,
                result.RmsError,
                result.Residuals,
                Samples = samples.Where(s=>s.Enabled && s.Source!="Placeholder").Select(s => s.Copy()).ToArray(),
                DisabledSamples = (archived??samples.Where(s=>!s.Enabled)).Select(s=>s.Copy()).ToArray(),
            };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document,
                new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(fullPath)) File.Replace(tempPath, fullPath, fullPath + ".bak");
            else File.Move(tempPath, fullPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public void ReplaceSamples(IEnumerable<NPointSample> samples)
    {
        lock (_sync)
        {
            var edited = samples.Select(p => p.Copy()).ToList();
            var replacement = edited.Where(s => s.Enabled).ToList();
            ArchiveReplaced(Samples, replacement, ArchivedSamples);
            ArchivedSamples.AddRange(edited.Where(s => !s.Enabled
                && !ArchivedSamples.Any(old => SamePoint(old, s))).Select(s => s.Copy()));
            Samples = replacement;
            SamplesContractVersion = 3;
            RuntimeResult = null;
            OnPropertyChanged(nameof(Samples));
            SampleRevision++;
            LastWrittenFingerprint = string.Empty;
        }
    }

    private static void ArchiveReplaced(List<NPointSample> current, List<NPointSample> replacement,
        List<NPointSample> archive)
    {
        foreach (var old in current)
        {
            if (replacement.Any(next => SamePoint(old, next)))
                continue;
            var retired = old.Copy();
            retired.Enabled = false;
            archive.Add(retired);
        }
    }

    private static bool SamePoint(NPointSample a, NPointSample b)
        => a.Id == b.Id && a.Kind == b.Kind && a.Enabled == b.Enabled
            && a.ImageX == b.ImageX && a.ImageY == b.ImageY
            && a.AxisX == b.AxisX && a.AxisY == b.AxisY && a.AxisAngle == b.AxisAngle;
}
