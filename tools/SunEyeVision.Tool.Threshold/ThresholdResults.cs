using System;
using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Tool.Threshold.Models;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 图像阈值化工具执行结果
    /// </summary>
    public class ThresholdResults : ToolResults
    {
        /// <summary>
        /// 输出图像（二值化后的图像）
        /// </summary>
        public Mat? OutputImage { get; set; }

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
        /// 灰度直方图数据（256个bin，值域0~1）
        /// </summary>
        public double[] HistogramData { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 通道均值（BGR三通道或灰度单通道）
        /// </summary>
        public double[] ChannelMeans { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 使用的自适应方法
        /// </summary>
        public AdaptiveMethod AdaptiveMethodUsed { get; set; }

        /// <summary>
        /// 使用的块大小
        /// </summary>
        public int BlockSizeUsed { get; set; }

        /// <summary>
        /// 是否反转
        /// </summary>
        public bool InvertUsed { get; set; }

        /// <summary>
        /// 输入图像尺寸
        /// </summary>
        public OpenCvSharp.Size InputSize { get; set; }

        /// <summary>
        /// 处理时间戳
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Blob 特征列表（连通域分析结果）
        /// </summary>
        public List<BlobInfo> Blobs { get; set; } = new();

        /// <summary>
        /// Blob 数量
        /// </summary>
        public int BlobCount => Blobs?.Count ?? 0;

        /// <summary>
        /// 获取可视化元素
        /// </summary>
        public override IEnumerable<VisualElement> GetVisualElements()
        {
            // 在图像左上角添加阈值参数信息标注
            if (InputSize.Width > 0 && InputSize.Height > 0)
            {
                // 背景矩形（半透明黑色）
                yield return VisualElement.Rectangle(
                    new OpenCvSharp.Rect2d(5, 5, 200, 55),
                    color: 0x80000000,
                    lineWidth: 0,
                    filled: true
                );

                // 阈值信息文本（白色）
                yield return VisualElement.Text(
                    text: $"阈值: {ThresholdUsed:F0}  最大值: {MaxValueUsed}",
                    position: new OpenCvSharp.Point2d(10, 22),
                    color: 0xFFFFFFFF,
                    fontSize: 12.0
                );

                // 阈值类型（绿色）
                yield return VisualElement.Text(
                    text: $"类型: {TypeUsed}",
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
                new("OutputImage",          "输出图像",               null),
                new("ThresholdUsed",        "实际使用的阈值",         "结果"),
                new("MaxValueUsed",         "最大值",                 "结果"),
                new("ThresholdUsedDouble",  "实际使用的阈值(Double)",  "结果"),
                new("MaxValueUsedDouble",   "最大值(Double)",          "结果"),
                new("HistogramData",        "直方图数据",              "统计"),
                new("ChannelMeans",         "通道均值",                "统计"),
                new("TypeUsed",             "阈值类型",               "结果"),
                new("AdaptiveMethodUsed",   "自适应方法",             "结果"),
                new("BlockSizeUsed",        "块大小",                 "结果"),
                new("InvertUsed",           "是否反转",               "结果"),
                new("InputSize",            "输入图像尺寸",           "结果"),
                new("ProcessedAt",          "处理时间戳",             "执行信息"),
                new("BlobCount",            "Blob数量",                "统计"),
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
            dict["AdaptiveMethodUsed"] = AdaptiveMethodUsed.ToString();
            dict["BlockSizeUsed"] = BlockSizeUsed;
            dict["InvertUsed"] = InvertUsed;
            dict["ProcessedAt"] = ProcessedAt;
            return dict;
        }
    }
}
