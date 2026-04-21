using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.BlobAnalysis.Models;

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
        /// 获取可视化元素
        /// </summary>
        public override IEnumerable<VisualElement> GetVisualElements()
        {
            if (InputSize.Width > 0 && InputSize.Height > 0)
            {
                // 背景矩形
                yield return VisualElement.Rectangle(
                    new OpenCvSharp.Rect2d(5, 5, 220, 55),
                    color: 0x80000000,
                    lineWidth: 0,
                    filled: true
                );

                // Blob统计信息文本
                yield return VisualElement.Text(
                    text: $"Blob: {FilteredBlobCount}/{TotalBlobCount}  阈值: {ThresholdUsed}",
                    position: new OpenCvSharp.Point2d(10, 22),
                    color: 0xFFFFFFFF,
                    fontSize: 12.0
                );

                // 面积信息
                yield return VisualElement.Text(
                    text: $"面积: {FilteredTotalArea:F0}  类型: {TypeUsed}",
                    position: new OpenCvSharp.Point2d(10, 42),
                    color: 0xFF00FF00,
                    fontSize: 11.0
                );
            }
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("BinaryImage",           "二值图像",               null),
                new("ThresholdUsed",         "实际使用的阈值",         "预处理"),
                new("MaxValueUsed",          "最大值",                 "预处理"),
                new("ThresholdUsedDouble",   "实际使用的阈值(Double)",  "预处理"),
                new("MaxValueUsedDouble",    "最大值(Double)",          "预处理"),
                new("TypeUsed",              "阈值类型",               "预处理"),
                new("InvertUsed",            "是否反转",               "预处理"),
                new("ConnectivityUsed",      "连通性类型",             "筛选"),
                new("TotalBlobCount",        "Blob总数",               "统计"),
                new("FilteredBlobCount",     "筛选后数量",             "统计"),
                new("TotalArea",             "总面积",                 "统计"),
                new("FilteredTotalArea",     "筛选后总面积",           "统计"),
                new("LargestBlobArea",       "最大Blob面积",           "统计"),
                new("SmallestBlobArea",      "最小Blob面积",           "统计"),
                new("HistogramData",         "直方图数据",             "统计"),
                new("ChannelMeans",          "通道均值",               "统计"),
                new("InputSize",             "输入图像尺寸",           "结果"),
                new("ProcessedAt",           "处理时间戳",             "执行信息"),
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
            dict["TotalBlobCount"] = TotalBlobCount;
            dict["FilteredBlobCount"] = FilteredBlobCount;
            dict["TotalArea"] = TotalArea;
            dict["FilteredTotalArea"] = FilteredTotalArea;
            dict["LargestBlobArea"] = LargestBlobArea;
            dict["SmallestBlobArea"] = SmallestBlobArea;
            dict["ProcessedAt"] = ProcessedAt;
            return dict;
        }
    }
}
