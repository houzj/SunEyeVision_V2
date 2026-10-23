using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.NPointCalibration;

public sealed class NPointCalibrationResults : ToolResults
{
    [JsonIgnore] public SunEyeVision.Plugin.SDK.Execution.Parameters.IResourcePathResolver? ResourcePaths { get; set; }
    public int TranslationSamples { get; set; }
    public int RotationSamples { get; set; }
    public int RequiredTranslationSamples { get; set; }
    public int RequiredRotationSamples { get; set; }
    public bool CalibrationReady { get; set; }
    public bool HasConvertedPoint { get; set; }
    public double CurrentImageX { get; set; }
    public double CurrentImageY { get; set; }
    public double MarkMachineX { get; set; }
    public double MarkMachineY { get; set; }
    public double MeanError { get; set; }
    public double MaxError { get; set; }
    public double RmsError { get; set; }
    public double TranslationRmsError { get; set; }
    public double RotationRmsError { get; set; }
    public double[] HomMat2D { get; set; } = Array.Empty<double>();
    public double AxisToMarkX { get; set; }
    public double AxisToMarkY { get; set; }
    public string SourceDeviceId { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ImageDomain { get; set; } = "Raw";
    public double ReferenceRAngle { get; set; }
    public int RotationSign { get; set; }
    public string AlgorithmId { get; set; } = SunEyeVision.Plugin.SDK.Models.Calibration.PlanarCalibrationFit.Algorithm;
    public double RotationSpanDeg { get; set; }
    public double RepeatErrorMm { get; set; }
    public bool TrainingPassed { get; set; }
    public string WorkPlaneId { get; set; } = string.Empty;
    [JsonIgnore] public string InputFingerprint { get; set; } = string.Empty;
    [JsonIgnore] public string AcceptedFingerprint { get; set; } = string.Empty;
    [JsonIgnore] public double[] AuxiliaryMatrix { get; set; } = Array.Empty<double>();
    [JsonIgnore] public System.Threading.CancellationToken Cancellation { get; set; }
    public List<NPointSampleResidual> Residuals { get; set; } = new();
    public string ProgressText => $"平移 {TranslationSamples}/{RequiredTranslationSamples}，旋转 {RotationSamples}/{RequiredRotationSamples}";

    // Runtime-only commit instruction. The workflow node applies it to the design
    // parameters after execution; snapshots and public outputs remain separate.
    [JsonIgnore] public NPointSample? CollectedSample { get; set; }
    [JsonIgnore] public bool NeedsCommit { get; set; }
    [JsonIgnore] public int ExpectedRevision { get; set; }

    public override List<PropertyMapEntry> GetPropertyMap() =>
    [
        .. base.GetPropertyMap(),
        new("TranslationSamples", "平移已采点数", "标定"),
        new("RotationSamples", "旋转已采点数", "标定"),
        new("CalibrationReady", "标定已求解", "标定"),
        new("HasConvertedPoint", "本轮已转换坐标", "运行"),
        new("CurrentImageX", "本轮图像X", "运行"),
        new("CurrentImageY", "本轮图像Y", "运行"),
        new("MarkMachineX", "Mark 机台 X", "运行"),
        new("MarkMachineY", "Mark 机台 Y", "运行"),
        new("MeanError", "平均误差", "标定"),
        new("MaxError", "最大误差", "标定"),
        new("RmsError", "整体 RMS", "标定"),
        new("HomMat2D", "像素到 Mark 机台矩阵", "标定"),
        new("AxisToMarkX", "轴心到 Mark 偏心 X", "标定"),
        new("AxisToMarkY", "轴心到 Mark 偏心 Y", "标定"),
        new("TrainingPassed", "训练质量通过", "标定"),
    ];
}

public sealed record NPointSampleResidual(int Id, NPointSampleKind Kind,
    double ErrorX, double ErrorY, double ErrorMm)
{
    public int DisplayId => Id + 1;
    public string KindText => Kind == NPointSampleKind.Translation ? "平移" : "旋转";
}
