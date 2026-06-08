using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具执行结果
    /// </summary>
    public class BlobAnalysisResults : ToolResults
    {
        /// <summary>
        /// 灰度输入图像（预处理后）
        /// </summary>
        public HImage? GrayImage { get; set; }

        /// <summary>
        /// 二值分割掩码图像（阈值得到的二值区域转图像）
        /// </summary>
        public HImage? BinaryMask { get; set; }

        /// <summary>
        /// 合成显示图像
        /// </summary>
        public HImage? OverlayImage { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            if (GrayImage != null && GrayImage.IsInitialized())
                images["GrayImage"] = new HalImage(GrayImage);
            if (BinaryMask != null && BinaryMask.IsInitialized())
                images["BinaryMask"] = new HalImage(BinaryMask);
            if (OverlayImage != null && OverlayImage.IsInitialized())
                images["OverlayImage"] = new HalImage(OverlayImage);
            return images;
        }

        /// <summary>
        /// 实际使用的阈值
        /// </summary>
        public int ThresholdUsed { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public int MaxValueUsed { get; set; }

        /// <summary>
        /// 使用的阈值类型（0=Binary, 1=Truncate...）
        /// </summary>
        public int TypeUsed { get; set; }

        /// <summary>
        /// 实际使用的阈值（Double 类型，用于参数绑定）
        /// </summary>
        public double ThresholdUsedDouble { get; set; }

        /// <summary>
        /// 最大值（Double 类型，用于参数绑定）
        /// </summary>
        public double MaxValueUsedDouble { get; set; }

        /// <summary>
        /// 是否反转
        /// </summary>
        public bool InvertUsed { get; set; }

        /// <summary>
        /// 灰度直方图数据（256个bin）
        /// </summary>
        public double[] HistogramData { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 通道均值
        /// </summary>
        public double[] ChannelMeans { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 输入图像尺寸
        /// </summary>
        public Size InputSize { get; set; }

        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 所有检测到的Blob（经过面积筛选和Per-Blob判断的最终集合）
        /// </summary>
        public List<BlobResult> FilteredBlobs { get; set; } = new();

        /// <summary>
        /// 筛选后Blob数量
        /// </summary>
        public int FilteredBlobCount => FilteredBlobs?.Count ?? 0;

        /// <summary>
        /// 筛选后Blob总面积
        /// </summary>
        public double FilteredTotalArea => FilteredBlobs?.Sum(b => b.Area) ?? 0;

        /// <summary>
        /// 筛选后平均面积
        /// </summary>
        public double AverageArea => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Area) : 0;

        /// <summary>
        /// 筛选后平均圆度
        /// </summary>
        public double AverageCircularity => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Circularity) : 0;

        /// <summary>
        /// 筛选后平均矩形度
        /// </summary>
        public double AverageRectangularity => FilteredBlobs?.Count > 0 ? FilteredBlobs.Average(b => b.Rectangularity) : 0;

        /// <summary>
        /// 白色像素比例(%)
        /// </summary>
        public double WhitePixelRatio { get; set; }

        #region 判断结果

        /// <summary>
        /// 所有判断是否通过
        /// </summary>
        public bool JudgmentPassed { get; set; } = true;

        /// <summary>
        /// 判断消息列表（每条判断的结果描述）
        /// </summary>
        public List<string> JudgmentMessages { get; set; } = new();

        #endregion

        #region Blob详细数据（数组类型）

        /// <summary>各Blob面积数组</summary>
        public double[] BlobAreas => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Area).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob周长数组</summary>
        public double[] BlobPerimeters => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Perimeter).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob中心X坐标数组</summary>
        public double[] BlobCentroidXs => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.CentroidX).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob中心Y坐标数组</summary>
        public double[] BlobCentroidYs => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.CentroidY).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob圆度数组</summary>
        public double[] BlobCircularities => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Circularity).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob矩形度数组</summary>
        public double[] BlobRectangularities => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.Rectangularity).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob边框数组（宽×高格式）</summary>
        public string[] BlobBoundingRects => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingRectText).ToArray()
            : Array.Empty<string>();

        /// <summary>各Blob边框宽度数组</summary>
        public double[] BlobBoundingWidths => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingBox.Width).ToArray()
            : Array.Empty<double>();

        /// <summary>各Blob边框高度数组</summary>
        public double[] BlobBoundingHeights => FilteredBlobs != null
            ? FilteredBlobs.Select(b => b.BoundingBox.Height).ToArray()
            : Array.Empty<double>();

        #endregion

        #region 图形结果（供下游节点数据绑定用）

        /// <summary>所有筛选通过Blob的外接矩形数组</summary>
        public WpfRect[] BlobBoundingBoxes { get; set; } = Array.Empty<WpfRect>();

        /// <summary>所有筛选通过Blob的轮廓点集数组</summary>
        public WpfPoint[][] BlobContours { get; set; } = Array.Empty<WpfPoint[]>();

        /// <summary>所有筛选通过Blob的质心点数组</summary>
        public WpfPoint[] BlobCentroids { get; set; } = Array.Empty<WpfPoint>();

        #endregion

        /// <summary>
        /// 获取结果显示项（用于数据面板显示）
        /// </summary>
    public override IReadOnlyList<ResultItem> GetResultItems()
    {
        return new List<ResultItem>
        {
            ResultItem.Boolean("判断通过", JudgmentPassed),
            ResultItem.Numeric("Blob数量", FilteredBlobCount),
            ResultItem.Numeric("总面积", FilteredTotalArea, "像素"),
            ResultItem.Numeric("平均面积", AverageArea, "像素"),
            ResultItem.Numeric("平均圆度", AverageCircularity),
            ResultItem.Numeric("平均矩形度", AverageRectangularity),
            ResultItem.Numeric("白色像素比例", WhitePixelRatio, "%"),
            ResultItem.Numeric("实际阈值", ThresholdUsed),
            ResultItem.Text("阈值类型", TypeUsed.ToString())
        };
    }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("GrayImage",                "灰度图像",               null),
                new("BinaryMask",               "二值掩码",               null),
                new("OverlayImage",             "合成显示图像",           null),
                new("ThresholdUsed",            "实际使用的阈值",         "预处理"),
                new("MaxValueUsed",             "最大值",                 "预处理"),
                new("ThresholdUsedDouble",      "实际使用的阈值(Double)", "预处理"),
                new("MaxValueUsedDouble",       "最大值(Double)",         "预处理"),
                new("TypeUsed",                 "阈值类型",               "预处理"),
                new("InvertUsed",               "是否反转",               "预处理"),
                new("JudgmentPassed",           "判断通过",               "判断"),
                new("FilteredBlobCount",        "Blob数量",               "统计"),
                new("FilteredTotalArea",        "总面积",                 "统计"),
                new("AverageArea",              "平均面积",               "统计"),
                new("AverageCircularity",       "平均圆度",               "统计"),
                new("AverageRectangularity",    "平均矩形度",             "统计"),
                new("WhitePixelRatio",          "白色像素比例",           "统计"),
                new("HistogramData",            "直方图数据",             "统计"),
                new("ChannelMeans",             "通道均值",               "统计"),
                new("InputSize",                "输入图像尺寸",           "结果"),
                // Blob详细数据（支持 / 分隔的层级分组）
                new("BlobAreas",                "面积",                   "Blob详细"),
                new("BlobPerimeters",           "周长",                   "Blob详细"),
                new("BlobCentroidXs",           "中心X",                  "Blob详细/中心"),
                new("BlobCentroidYs",           "中心Y",                  "Blob详细/中心"),
                new("BlobCircularities",        "圆度",                   "Blob详细/形状"),
                new("BlobRectangularities",     "矩形度",                 "Blob详细/形状"),
                new("BlobBoundingWidths",       "边框宽",                 "Blob详细/边框"),
                new("BlobBoundingHeights",      "边框高",                 "Blob详细/边框"),
                new("BlobBoundingRects",        "边框(文本)",             "Blob详细/边框"),
                new("ProcessedAt",              "处理时间戳",             "执行信息"),
                // 图形结果（供下游节点绑定显示）
                new("BlobBoundingBoxes",         "外接矩形",               "图形结果"),
                new("BlobContours",              "轮廓",                   "图形结果"),
                new("BlobCentroids",             "质心",                   "图形结果"),
            ];
        }

        /// <summary>
        /// 转换为字典
        /// </summary>
        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["ThresholdUsed"] = ThresholdUsed;
            dict["MaxValueUsed"] = MaxValueUsed;
            dict["TypeUsed"] = TypeUsed.ToString();
            dict["InvertUsed"] = InvertUsed;
            dict["JudgmentPassed"] = JudgmentPassed;
            dict["FilteredBlobCount"] = FilteredBlobCount;
            dict["FilteredTotalArea"] = FilteredTotalArea;
            dict["AverageArea"] = AverageArea;
            dict["AverageCircularity"] = AverageCircularity;
            dict["AverageRectangularity"] = AverageRectangularity;
            dict["WhitePixelRatio"] = WhitePixelRatio;
            dict["ProcessedAt"] = ProcessedAt;
            dict["BlobBoundingBoxes"] = BlobBoundingBoxes;
            dict["BlobContours"] = BlobContours;
            dict["BlobCentroids"] = BlobCentroids;
            return dict;
        }
    }
}
