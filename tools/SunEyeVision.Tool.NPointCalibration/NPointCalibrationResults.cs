using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.NPointCalibration;

public sealed class NPointCalibrationResults : ToolResults
{
    public int TranslationSamples { get; set; }
    public int RotationSamples { get; set; }
    public int RequiredTranslationSamples { get; set; }
    public int RequiredRotationSamples { get; set; }
    public bool CalibrationReady { get; set; }
    public string CoordinateMode { get; set; } = "Absolute";
    public bool HasConvertedPoint { get; set; }
    public double CurrentImageX { get; set; }
    public double CurrentImageY { get; set; }
    public double ConvertedPhysicalX { get; set; }
    public double ConvertedPhysicalY { get; set; }
    public double MeanError { get; set; }
    public double MaxError { get; set; }
    public double[] HomMat2D { get; set; } = Array.Empty<double>();
    public double RotationCenterImageX { get; set; }
    public double RotationCenterImageY { get; set; }
    public double RotationCenterPhysicalX { get; set; }
    public double RotationCenterPhysicalY { get; set; }
    public string ProgressText => $"平移 {TranslationSamples}/{RequiredTranslationSamples}，旋转 {RotationSamples}/{RequiredRotationSamples}";

    // Runtime-only commit instruction. The workflow node applies it to the design
    // parameters after execution; snapshots and public outputs remain separate.
    [JsonIgnore] public NPointSample? CollectedSample { get; set; }
    [JsonIgnore] public bool NeedsCommit { get; set; }
    [JsonIgnore] public bool TeachSignalActive { get; set; }
    [JsonIgnore] public long TeachEventId { get; set; }
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
        new("ConvertedPhysicalX", "转换物理X", "运行"),
        new("ConvertedPhysicalY", "转换物理Y", "运行"),
        new("MeanError", "平均误差", "标定"),
        new("MaxError", "最大误差", "标定"),
        new("HomMat2D", "像素到物理矩阵", "标定"),
        new("RotationCenterPhysicalX", "旋转中心物理X", "标定"),
        new("RotationCenterPhysicalY", "旋转中心物理Y", "标定"),
    ];
}
