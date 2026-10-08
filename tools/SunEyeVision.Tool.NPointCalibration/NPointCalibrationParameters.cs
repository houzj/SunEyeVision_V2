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

namespace SunEyeVision.Tool.NPointCalibration;

public enum NPointAcquisitionMode { Trigger, Manual }
public enum NPointInputMode { Point, Coordinates }
public enum NPointCameraMode { FixedAbove, FixedBelow, Moving }
public enum NPointTransformModel { Similarity, Affine, Projective }
public enum NPointWeightFunction { LeastSquares, Huber, Tukey }
public enum NPointSampleKind { Translation, Rotation }

public sealed class NPointSample
{
    public int Id { get; set; }
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
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore] public string StatusText => Source == "Placeholder" ? "待采集" : Enabled ? "有效" : "停用";
    [JsonIgnore] public string ImageXDisplay => Display(ImageX);
    [JsonIgnore] public string ImageYDisplay => Display(ImageY);
    [JsonIgnore] public string AxisXDisplay => Display(AxisX);
    [JsonIgnore] public string AxisYDisplay => Display(AxisY);
    [JsonIgnore] public string AxisAngleDisplay => Display(AxisAngle);
    private string Display(double value) => Source == "Placeholder" ? string.Empty : value.ToString("F3");

    public NPointSample Copy() => (NPointSample)MemberwiseClone();
}

[JsonDerivedType(typeof(NPointCalibrationParameters), "NPointCalibration")]
public class NPointCalibrationParameters : ToolParameters, IWorkflowResultCommitter
{
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
    /// <summary>Bind to the image source FrameTimestampText (ISO 8601).</summary>
    public string FrameTimestampText { get; set; } = string.Empty;
    public string FrameIdText { get; set; } = string.Empty;
    /// <summary>UTC Unix milliseconds of the same machine XYR readback.</summary>
    public long AxisReadUnixMs { get; set; }
    public int MaxPairingDelayMs { get; set; } = 100;
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
    /// <summary>Optional monotonically increasing event ID. Zero uses rising-edge detection.</summary>
    public long TeachEventId { get; set; }
    public long LastTeachEventId { get; set; }
    public bool LastTeachSignal { get; set; }
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
    private NPointWeightFunction _weightFunction = NPointWeightFunction.LeastSquares;
    public NPointWeightFunction WeightFunction
    {
        get => _weightFunction;
        set
        {
            if (SetProperty(ref _weightFunction, value))
                OnPropertyChanged(nameof(IsRobustWeight));
        }
    }
    [JsonIgnore] public bool IsRobustWeight => WeightFunction != NPointWeightFunction.LeastSquares;
    /// <summary>Dimensionless multiplier of the estimated physical residual scale.</summary>
    public double WeightCoefficient { get; set; } = 20;
    public double RobustScaleFloorMm { get; set; } = 0.001;
    /// <summary>Site-specific maximum training-point residual; required before writing a candidate.</summary>
    public double MaximumTrainingErrorMm { get; set; }

    public List<NPointSample> Samples { get; set; } = new();
    public List<NPointSample> ArchivedSamples { get; set; } = new();
    public int SampleRevision { get; set; }
    public int LastWrittenRevision { get; set; } = -1;

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
            if (calibration.NeedsCommit && SampleRevision != calibration.ExpectedRevision)
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
            }
            if (calibration.NeedsCommit)
            {
                LastTeachSignal = calibration.TeachSignalActive;
                if (calibration.CollectedSample != null && calibration.TeachEventId > 0)
                    LastTeachEventId = calibration.TeachEventId;
                SampleRevision++;
                if (calibration.CollectedSample != null)
                    OnPropertyChanged(nameof(Samples));
            }
            if (calibration.CalibrationReady && UpdateFileAfterSolve
                && LastWrittenRevision != SampleRevision)
            {
                try
                {
                    WriteCandidateFile(CalibrationFilePath, calibration, Samples, MaximumTrainingErrorMm);
                    LastWrittenRevision = SampleRevision;
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

    public static string CandidatePath(string activityPath)
    {
        string fullPath = Path.GetFullPath(activityPath);
        return Path.Combine(Path.GetDirectoryName(fullPath)!,
            Path.GetFileNameWithoutExtension(fullPath) + ".candidate.json");
    }

    public static void WriteCandidateFile(string activityPath, NPointCalibrationResults result,
        IEnumerable<NPointSample> samples, double maximumTrainingErrorMm)
    {
        if (!result.CalibrationReady || result.HomMat2D.Length != 6)
            throw new InvalidOperationException("联合标定尚未完成，不能生成候选文件。");
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
                SchemaVersion = 2,
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
                result.WeightFunction,
                result.WeightCoefficient,
                result.SourceDeviceId,
                result.ImageWidth,
                result.ImageHeight,
                result.ImageDomain,
                result.RobustScaleMm,
                result.RobustThresholdMm,
                result.MeanError,
                result.MaxError,
                result.RmsError,
                result.Residuals,
                Samples = samples.Select(s => s.Copy()).ToArray(),
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
            OnPropertyChanged(nameof(Samples));
            if (Samples.Count == 0)
            {
                LastTeachSignal = TeachSignal || (TeachEnabled
                    && !string.IsNullOrWhiteSpace(TeachTriggerText)
                    && string.Equals(ExternalInputText, TeachTriggerText, StringComparison.Ordinal));
                LastTeachEventId = TeachEventId;
            }
            SampleRevision++;
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
