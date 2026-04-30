using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob分析工具执行结果
    /// </summary>
    public class BlobAnalysisResults : ToolResults
    {
        /// <summary>
        /// 二值化图像（预处理输出）
        /// </summary>
        public Mat? BinaryImage { get; set; }

        /// <summary>
        /// 输出图像（兼容 AlgorithmNode 反射查找）
        /// </summary>
        public Mat? OutputImage => BinaryImage;

        /// <summary>
        /// 获取输出图像（DispImage通道）
        /// </summary>
        public override Mat? GetOutputImage() => BinaryImage;

        /// <summary>
        /// 实际使用的阈值
        /// </summary>
        public int ThresholdUsed { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        public int MaxValueUsed { get; set; }

        /// <summary>
        /// 使用的阈值类型
        /// </summary>
        public ThresholdType TypeUsed { get; set; }

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
        /// 连通性类型
        /// </summary>
        public ConnectivityType ConnectivityUsed { get; set; }

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
        public OpenCvSharp.Size InputSize { get; set; }

        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 所有检测到的Blob
        /// </summary>
        public List<BlobInfo> AllBlobs { get; set; } = new();

        /// <summary>
        /// 筛选后的Blob
        /// </summary>
        public List<BlobInfo> FilteredBlobs { get; set; } = new();

        /// <summary>
        /// 所有Blob数量
        /// </summary>
        public int TotalBlobCount => AllBlobs?.Count ?? 0;

        /// <summary>
        /// 筛选后Blob数量
        /// </summary>
        public int FilteredBlobCount => FilteredBlobs?.Count ?? 0;

        /// <summary>
        /// 所有Blob总面积
        /// </summary>
        public double TotalArea => AllBlobs?.Sum(b => b.Area) ?? 0;

        /// <summary>
        /// 筛选后Blob总面积
        /// </summary>
        public double FilteredTotalArea => FilteredBlobs?.Sum(b => b.Area) ?? 0;

        /// <summary>
        /// 最大Blob面积
        /// </summary>
        public double LargestBlobArea => AllBlobs?.Count > 0 ? AllBlobs.Max(b => b.Area) : 0;

        /// <summary>
        /// 最小Blob面积
        /// </summary>
        public double SmallestBlobArea => AllBlobs?.Count > 0 ? AllBlobs.Min(b => b.Area) : 0;

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
            return items;
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("BinaryImage",              "二值图像",               null),
                new("ThresholdUsed",            "实际使用的阈值",         "预处理"),
                new("MaxValueUsed",             "最大值",                 "预处理"),
                new("ThresholdUsedDouble",      "实际使用的阈值(Double)", "预处理"),
                new("MaxValueUsedDouble",       "最大值(Double)",         "预处理"),
                new("TypeUsed",                 "阈值类型",               "预处理"),
                new("InvertUsed",               "是否反转",               "预处理"),
                new("ConnectivityUsed",         "连通性类型",             "筛选"),
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
                new("HistogramData",            "直方图数据",             "统计"),
                new("ChannelMeans",             "通道均值",               "统计"),
                new("InputSize",                "输入图像尺寸",           "结果"),
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
            dict["ConnectivityUsed"] = ConnectivityUsed.ToString();
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
