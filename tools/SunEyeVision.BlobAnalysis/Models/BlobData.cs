using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Tool.BlobAnalysis.Models;

/// <summary>
/// Blob 特征数据（纯 Halcon/SDK 类型，无 OpenCvSharp 依赖）
/// 替代 CvEngine.Models.BlobInfo
/// </summary>
public sealed class BlobData
{
    public int Label { get; set; }
    public double Area { get; set; }
    public double Perimeter { get; set; }
    public double CentroidX { get; set; }
    public double CentroidY { get; set; }
    public Rect2d BoundingBox { get; set; }
    public double Circularity { get; set; }
    public double Rectangularity { get; set; }

    /// <summary>
    /// 轮廓点集（从 Halcon HXLDCont 提取，用于 DisplayCollector.AddContours）
    /// </summary>
    public Point2d[]? ContourPoints { get; set; }

    /// <summary>
    /// Per-Blob 判断是否通过（控制单个 blob 显示颜色）
    /// </summary>
    public bool JudgmentPassed { get; set; } = true;

    /// <summary>
    /// 边框文本描述
    /// </summary>
    public string BoundingRectText =>
        $"({BoundingBox.X:F0},{BoundingBox.Y:F0}) {BoundingBox.Width:F0}x{BoundingBox.Height:F0}";
}
