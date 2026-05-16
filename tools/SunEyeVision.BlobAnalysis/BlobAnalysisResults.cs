using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.BlobAnalysis.Models;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具执行结果（Halcon 原生类型）
    /// </summary>
    public class BlobAnalysisResults : ToolResults
    {
        /// <summary>
        /// 灰度输入图像（预处理后）
        /// </summary>
        public HImage? GrayImage { get; set; }

        /// <summary>
        /// 二值分割掩码图像（Halcon HImage, 0/255）
        /// </summary>
        public HImage? BinaryMask { get; set; }

        /// <summary>
        /// 合成显示图像（灰度+二值叠加效果）
        /// </summary>
        public HImage? OverlayImage { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, HImage?> GetDisplayImages()
        {
            return new()
            {
                ["GrayImage"] = GrayImage,
                ["BinaryMask"] = BinaryMask,
                ["OverlayImage"] = OverlayImage
            };
        }

        /// <summary>
        /// 图像宽度
        /// </summary>
        public int ImageWidth { get; set; }

        /// <summary>
        /// 图像高度
        /// </summary>
        public int ImageHeight { get; set; }

        /// <summary>
        /// 实际使用的阈值
        /// </summary>
        public double ThresholdUsed { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public double MaxValueUsed { get; set; }

        /// <summary>
        /// 使用的阈值类型
        /// </summary>
        public ThresholdType TypeUsed { get; set; }

        /// <summary>
        /// 是否反转
        /// </summary>
        public bool InvertUsed { get; set; }

        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 筛选后的Blob
    /// </summary>
    public List<BlobData> FilteredBlobs { get; set; } = new();

    /// <summary>
    /// 所有Blob数量（等于筛选后数量）
    /// </summary>
    public int TotalBlobCount => FilteredBlobs?.Count ?? 0;

    /// <summary>
    /// 筛选后Blob数量
    /// </summary>
    public int FilteredBlobCount => FilteredBlobs?.Count ?? 0;

    /// <summary>
    /// 所有Blob总面积（等于筛选后总面积）
    /// </summary>
    public double TotalArea => FilteredTotalArea;

        /// <summary>
        /// 筛选后Blob总面积
        /// </summary>
        public double FilteredTotalArea => FilteredBlobs?.Sum(b => b.Area) ?? 0;

    /// <summary>
    /// 最大Blob面积
    /// </summary>
    public double LargestBlobArea => FilteredBlobs?.Count > 0 ? FilteredBlobs.Max(b => b.Area) : 0;

    /// <summary>
    /// 最小Blob面积
    /// </summary>
    public double SmallestBlobArea => FilteredBlobs?.Count > 0 ? FilteredBlobs.Min(b => b.Area) : 0;

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

        #endregion

        /// <summary>
        /// 获取结果显示项（用于数据面板显示）
        /// </summary>
        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>
            {
                ResultItem.Boolean("判断通过", JudgmentPassed),
                ResultItem.Numeric("Blob总数", TotalBlobCount),
                ResultItem.Numeric("筛选后数量", FilteredBlobCount),
                ResultItem.Numeric("总面积", FilteredTotalArea, "像素"),
                ResultItem.Numeric("最大面积", LargestBlobArea, "像素"),
                ResultItem.Numeric("最小面积", SmallestBlobArea, "像素"),
                ResultItem.Numeric("平均面积", AverageArea, "像素"),
                ResultItem.Numeric("平均圆度", AverageCircularity),
                ResultItem.Numeric("平均矩形度", AverageRectangularity),
                ResultItem.Numeric("白色像素比例", WhitePixelRatio, "%"),
                ResultItem.Numeric("实际阈值", ThresholdUsed),
                ResultItem.Text("阈值类型", TypeUsed.ToString())
            };

            // 每个Blob属性的数组输出
            if (FilteredBlobs != null && FilteredBlobs.Count > 0)
            {
                items.Add(ResultItem.Text("面积", string.Join(", ", FilteredBlobs.Select(b => $"{b.Area:F0}"))));
                items.Add(ResultItem.Text("周长", string.Join(", ", FilteredBlobs.Select(b => $"{b.Perimeter:F1}"))));
                items.Add(ResultItem.Text("中心X", string.Join(", ", FilteredBlobs.Select(b => $"{b.CentroidX:F1}"))));
                items.Add(ResultItem.Text("中心Y", string.Join(", ", FilteredBlobs.Select(b => $"{b.CentroidY:F1}"))));
                items.Add(ResultItem.Text("圆度", string.Join(", ", FilteredBlobs.Select(b => $"{b.Circularity:F3}"))));
                items.Add(ResultItem.Text("矩形度", string.Join(", ", FilteredBlobs.Select(b => $"{b.Rectangularity:F3}"))));
                items.Add(ResultItem.Text("边框", string.Join(", ", FilteredBlobs.Select(b => b.BoundingRectText))));
                items.Add(ResultItem.Text("判断", string.Join(", ", FilteredBlobs.Select(b => b.JudgmentPassed ? "✓" : "✗"))));
            }

            return items;
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
                new("TypeUsed",                 "阈值类型",               "预处理"),
                new("InvertUsed",               "是否反转",               "预处理"),
                new("JudgmentPassed",           "判断通过",               "判断"),
                new("TotalBlobCount",           "Blob总数",               "统计"),
                new("FilteredBlobCount",        "筛选后数量",             "统计"),
                new("TotalArea",                "总面积",                 "统计"),
                new("FilteredTotalArea",        "筛选后总面积",           "统计"),
                new("LargestBlobArea",          "最大Blob面积",           "统计"),
                new("SmallestBlobArea",         "最小Blob面积",           "统计"),
                new("AverageArea",              "平均面积",               "统计"),
                new("AverageCircularity",       "平均圆度",               "统计"),
                new("AverageRectangularity",    "平均矩形度",             "统计"),
                new("WhitePixelRatio",          "白色像素比例",           "统计"),
                new("ImageWidth",               "图像宽度",               "结果"),
                new("ImageHeight",              "图像高度",               "结果"),
                // Blob详细数据（分号分隔数组）
                new("BlobAreas",                "面积",                   "Blob详细"),
                new("BlobPerimeters",           "周长",                   "Blob详细"),
                new("BlobCentroidXs",           "中心X",                  "Blob详细"),
                new("BlobCentroidYs",           "中心Y",                  "Blob详细"),
                new("BlobCircularities",        "圆度",                   "Blob详细"),
                new("BlobRectangularities",     "矩形度",                 "Blob详细"),
                new("BlobBoundingRects",        "边框",                   "Blob详细"),
                new("ProcessedAt",              "处理时间戳",             "执行信息"),
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
            dict["TotalBlobCount"] = TotalBlobCount;
            dict["FilteredBlobCount"] = FilteredBlobCount;
            dict["TotalArea"] = TotalArea;
            dict["FilteredTotalArea"] = FilteredTotalArea;
            dict["LargestBlobArea"] = LargestBlobArea;
            dict["SmallestBlobArea"] = SmallestBlobArea;
            dict["AverageArea"] = AverageArea;
            dict["AverageCircularity"] = AverageCircularity;
            dict["AverageRectangularity"] = AverageRectangularity;
            dict["WhitePixelRatio"] = WhitePixelRatio;
            dict["ProcessedAt"] = ProcessedAt;
            return dict;
        }
    }
}
