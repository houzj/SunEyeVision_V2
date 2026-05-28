using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.ShapeMatch.Models;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace SunEyeVision.Tool.ShapeMatch;

/// <summary>
/// 轮廓匹配工具执行结果
/// </summary>
public class ShapeMatchResults : ToolResults
{
    // ===== 输出图像 =====
    [System.Text.Json.Serialization.JsonIgnore]
    public HImage? OutputImage { get; set; }

    // ===== 调试中间图像 =====
    [System.Text.Json.Serialization.JsonIgnore]
    public HImage? DebugGrayImage { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public HImage? DebugSearchRegionMask { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public HImage? DebugMagnitude { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public HImage? DebugEdgeMap { get; set; }

    /// <summary>匹配到的结果列表</summary>
    public List<ShapeMatchResultItem> MatchedItems { get; set; } = new();

    /// <summary>匹配数量</summary>
    public int MatchCount => MatchedItems?.Count ?? 0;

    /// <summary>是否找到匹配</summary>
    public bool HasMatch => MatchCount > 0;

    /// <summary>最佳匹配得分</summary>
    public double BestScore => MatchedItems?.Count > 0 ? MatchedItems.Max(m => m.Score) : 0;

    /// <summary>最佳匹配项</summary>
    public ShapeMatchResultItem? BestMatch => MatchedItems?.Count > 0 ? MatchedItems.OrderByDescending(m => m.Score).First() : null;

    /// <summary>输入图像尺寸</summary>
    public System.Drawing.Size InputSize { get; set; }

    /// <summary>处理时间戳</summary>
    public DateTime ProcessedAt { get; set; } = DateTime.Now;

    #region 匹配详细数据（数组类型 — 对齐 BlobAnalysis）

    public double[] MatchedScores => MatchedItems.Select(m => m.Score).ToArray();
    public double[] MatchedCenterXs => MatchedItems.Select(m => m.CenterX).ToArray();
    public double[] MatchedCenterYs => MatchedItems.Select(m => m.CenterY).ToArray();
    public double[] MatchedAngles => MatchedItems.Select(m => m.Angle).ToArray();
    public double[] MatchedScales => MatchedItems.Select(m => m.Scale).ToArray();
    public int[]    MatchedTemplateIndices => MatchedItems.Select(m => m.TemplateOrderIndex).ToArray();
    public string[] MatchedTemplateNames => MatchedItems.Select(m => m.TemplateName).ToArray();

    #endregion

    #region 位置修正（参考图像→测试图像方向，已扣除模板参考点）

    /// <summary>
    /// 位置修正矩阵（参考图像→测试图像方向）
    /// 已扣除模板参考点偏移，下游工具直接正向使用
    /// </summary>
    public AffineMatrix BestPositionCorrection =>
        BestMatch != null
            ? new AffineMatrix(
                BestMatch.TransformMatrix.M11,
                BestMatch.TransformMatrix.M12,
                BestMatch.TransformMatrix.M21,
                BestMatch.TransformMatrix.M22,
                BestMatch.TransformMatrix.OffsetX - BestMatch.ReferencePointX,
                BestMatch.TransformMatrix.OffsetY - BestMatch.ReferencePointY)
            : AffineMatrix.Identity;

    /// <summary>
    /// 所有匹配的位置修正矩阵数组（工作流多实例展开用）
    /// 已扣除模板参考点偏移
    /// </summary>
    public AffineMatrix[] MatchedPositionCorrections =>
        MatchedItems?.Select(m => new AffineMatrix(
            m.TransformMatrix.M11,
            m.TransformMatrix.M12,
            m.TransformMatrix.M21,
            m.TransformMatrix.M22,
            m.TransformMatrix.OffsetX - m.ReferencePointX,
            m.TransformMatrix.OffsetY - m.ReferencePointY)).ToArray()
        ?? Array.Empty<AffineMatrix>();

    #endregion

    #region 判断结果

    public bool JudgmentPassed { get; set; } = true;
    public List<string> JudgmentMessages { get; set; } = new();

    #endregion

    /// <summary>
    /// 获取所有可显示的图像
    /// </summary>
    public override Dictionary<string, IVisionImage?> GetDisplayImages()
    {
        var images = new Dictionary<string, IVisionImage?>();
        if (OutputImage?.IsInitialized() == true)
            images["OutputImage"] = new HalImage(OutputImage);
        if (DebugGrayImage?.IsInitialized() == true)
            images["DebugGrayImage"] = new HalImage(DebugGrayImage);
        if (DebugSearchRegionMask?.IsInitialized() == true)
            images["DebugSearchRegionMask"] = new HalImage(DebugSearchRegionMask);
        if (DebugMagnitude?.IsInitialized() == true)
            images["DebugMagnitude"] = new HalImage(DebugMagnitude);
        if (DebugEdgeMap?.IsInitialized() == true)
            images["DebugEdgeMap"] = new HalImage(DebugEdgeMap);
        return images;
    }

    public override IReadOnlyList<ResultItem> GetResultItems()
    {
        return new List<ResultItem>
        {
            ResultItem.Boolean("判断通过", JudgmentPassed),
            ResultItem.Numeric("匹配数量", MatchCount),
            ResultItem.Numeric("最佳得分", BestScore)
        };
    }

    public override List<PropertyMapEntry> GetPropertyMap()
    {
        return
        [
            new("OutputImage",           "输出图像",           null),
            new("DebugGrayImage",        "灰度图像(调试)",     null),
            new("DebugSearchRegionMask", "搜索区域掩码(调试)", null),
            new("JudgmentPassed",        "判断通过",           "判断"),
            new("MatchCount",            "匹配数量",           "结果"),
            new("BestScore",             "最佳得分",           "结果"),
            new("BestPositionCorrection","位置修正矩阵",       "位置修正"),
            new("MatchedPositionCorrections","位置修正矩阵列表", "位置修正"),
            new("InputSize",             "输入图像尺寸",       "结果"),
            new("MatchedScores",         "得分",               "匹配详细/得分"),
            new("MatchedCenterXs",       "中心X",              "匹配详细/中心"),
            new("MatchedCenterYs",       "中心Y",              "匹配详细/中心"),
            new("MatchedAngles",         "角度",               "匹配详细/姿态"),
            new("MatchedScales",         "缩放",               "匹配详细/姿态"),
            new("MatchedTemplateIndices","模板索引",           "匹配详细/模板信息"),
            new("MatchedTemplateNames",  "模板名称",           "匹配详细/模板信息"),
            new("ProcessedAt",           "处理时间戳",         "执行信息"),
        ];
    }

    public override Dictionary<string, object> ToDictionary()
    {
        var dict = base.ToDictionary();
        dict["JudgmentPassed"] = JudgmentPassed;
        dict["MatchCount"] = MatchCount;
        dict["HasMatch"] = HasMatch;
        dict["BestScore"] = BestScore;
        dict["BestPositionCorrection"] = BestPositionCorrection;
        dict["MatchedPositionCorrections"] = MatchedPositionCorrections;
        dict["InputSize"] = InputSize;
        dict["ProcessedAt"] = ProcessedAt;
        return dict;
    }
}

/// <summary>
/// 单个匹配结果项
/// </summary>
public class ShapeMatchResultItem
{
    /// <summary>匹配分数（0~1）</summary>
    public double Score { get; set; }

    /// <summary>中心X坐标</summary>
    public double CenterX { get; set; }

    /// <summary>中心Y坐标</summary>
    public double CenterY { get; set; }

    /// <summary>旋转角度（度）</summary>
    public double Angle { get; set; }

    /// <summary>缩放比例</summary>
    public double Scale { get; set; }

    /// <summary>匹配到的模板名称</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>匹配到的模板ID</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>模板在参数列表中的序号（从0开始）</summary>
    public int TemplateOrderIndex { get; set; }

    /// <summary>包围矩形</summary>
    public WpfRect? BoundingBox { get; set; }

    /// <summary>原始模板轮廓点（模型坐标系，未变换）</summary>
    public List<WpfPoint>? ContourPoints { get; set; }

    /// <summary>原始模板轮廓链组（每链独立，未压平）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<WpfPoint[]>? ContourChains { get; set; }

    /// <summary>仿射变换矩阵（将模型坐标变换到图像坐标）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Windows.Media.Matrix TransformMatrix { get; set; }

    /// <summary>模板参考点 X（训练时模板在参考图像中的列坐标）</summary>
    public double ReferencePointX { get; set; }

    /// <summary>模板参考点 Y（训练时模板在参考图像中的行坐标）</summary>
    public double ReferencePointY { get; set; }

    /// <summary>轮廓点数量</summary>
    public int ContourPointCount => ContourPoints?.Count ?? 0;

    /// <summary>该匹配项是否通过判断</summary>
    public bool JudgmentPassed { get; set; } = true;
}
