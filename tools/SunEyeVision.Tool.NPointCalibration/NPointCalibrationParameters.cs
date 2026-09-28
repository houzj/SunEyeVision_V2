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
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PhysicalX { get; set; }
    public double PhysicalY { get; set; }
    public double ImageAngle { get; set; }
    public double PhysicalAngle { get; set; }
    public bool Enabled { get; set; } = true;
    public long? FrameId { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

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
    public Point2d[] PhysicalPoint { get; set; } = Array.Empty<Point2d>();
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PhysicalX { get; set; }
    public double PhysicalY { get; set; }
    public double ImageAngle { get; set; }
    public double PhysicalAngle { get; set; }

    /// <summary>Upstream signal; a false-to-true transition captures one pair.</summary>
    public bool TeachSignal { get; set; }
    public bool TeachEnabled { get; set; }
    public string ExternalInputText { get; set; } = string.Empty;
    public string TeachTriggerText { get; set; } = "TEACH";
    /// <summary>Optional monotonically increasing event ID. Zero uses rising-edge detection.</summary>
    public long TeachEventId { get; set; }
    public long LastTeachEventId { get; set; }
    public bool LastTeachSignal { get; set; }
    public bool UseGeneratedPhysical { get; set; }
    public int TranslationCount { get; set; } = 9;
    public int RotationCount { get; set; } = 3;
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double OffsetX { get; set; } = 1;
    public double OffsetY { get; set; } = 1;
    public bool XFirst { get; set; } = true;
    public int ReverseAfter { get; set; } = 3;
    public double BaseAngle { get; set; }
    public double AngleStep { get; set; } = 1;
    public bool UseRelativeCoordinates { get; set; }
    public int OriginIndex { get; set; } = 4;

    private string _calibrationFilePath = string.Empty;
    public string CalibrationFilePath
    {
        get => _calibrationFilePath;
        set => SetProperty(ref _calibrationFilePath, value);
    }
    public bool UpdateFileAfterSolve { get; set; }
    public NPointCameraMode CameraMode { get; set; } = NPointCameraMode.FixedAbove;
    public NPointTransformModel TransformModel { get; set; } = NPointTransformModel.Affine;
    public NPointWeightFunction WeightFunction { get; set; } = NPointWeightFunction.LeastSquares;
    public double WeightCoefficient { get; set; } = 20;

    public List<NPointSample> Samples { get; set; } = new();
    public int SampleRevision { get; set; }
    public int LastWrittenRevision { get; set; } = -1;

    public override void CopyFrom(ToolParameters other)
    {
        base.CopyFrom(other);
        if (other is NPointCalibrationParameters source)
        {
            lock (source._sync)
                Samples = source.Samples.Select(sample => sample.Copy()).ToList();
        }
    }

    [JsonIgnore]
    public string ImagePointBindingSource { get; set; } = string.Empty;
    [JsonIgnore]
    public string PhysicalPointBindingSource { get; set; } = string.Empty;

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
            }
            if (calibration.CalibrationReady && UpdateFileAfterSolve
                && LastWrittenRevision != SampleRevision)
            {
                if (string.IsNullOrWhiteSpace(CalibrationFilePath))
                {
                    error = "已开启更新标定文件，但未设置文件路径。";
                    return false;
                }
                try
                {
                    WriteCalibrationFile(CalibrationFilePath, calibration, Samples);
                    LastWrittenRevision = SampleRevision;
                }
                catch (Exception ex)
                {
                    error = $"标定点已记录，但更新标定文件失败：{ex.Message}";
                    return false;
                }
            }
        }
        return true;
    }

    public static void WriteCalibrationFile(string path, NPointCalibrationResults result,
        IEnumerable<NPointSample> samples)
    {
        if (!result.CalibrationReady || result.HomMat2D.Length == 0)
            throw new InvalidOperationException("点集未完成求解，不能生成标定文件。");
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tempPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            var document = new
            {
                SchemaVersion = 1,
                Direction = "ImageColumnRowToPhysicalXY",
                PhysicalUnit = "mm",
                result.CoordinateMode,
                CreatedAt = DateTimeOffset.UtcNow,
                Matrix = result.HomMat2D,
                result.MeanError,
                result.MaxError,
                result.RotationCenterImageX,
                result.RotationCenterImageY,
                result.RotationCenterPhysicalX,
                result.RotationCenterPhysicalY,
                Samples = samples.Select(s => s.Copy()).ToArray(),
            };
            File.WriteAllText(tempPath, JsonSerializer.Serialize(document,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, fullPath, true);
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
            Samples = samples.Select(p => p.Copy()).ToList();
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
}
