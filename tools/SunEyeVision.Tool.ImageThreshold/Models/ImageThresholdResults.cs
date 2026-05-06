using System;
using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;

namespace SunEyeVision.Tool.ImageThreshold.Models
{
    /// <summary>
    /// 图像二值化工具执行结果
    /// </summary>
    public class ImageThresholdResults : ToolResults
    {
        /// <summary>
        /// 输出图像（二值化后的图像）
        /// </summary>
        public Mat? OutputImage { get; set; }

        /// <summary>
        /// 实际使用的二值化方法
        /// </summary>
        public ThresholdMethod MethodUsed { get; set; }

        /// <summary>
        /// 实际使用的阈值
        /// </summary>
        public double ThresholdUsed { get; set; }

        /// <summary>
        /// 实际使用的最大值
        /// </summary>
        public double MaxValueUsed { get; set; }

        /// <summary>
        /// 白色像素数
        /// </summary>
        public long WhitePixelCount { get; set; }

        /// <summary>
        /// 白色像素比例(%)
        /// </summary>
        public double WhitePixelRatio { get; set; }

        /// <summary>
        /// 区域个数（连通域数）
        /// </summary>
        public int RegionCount { get; set; }

        /// <summary>
        /// 输出图像均值
        /// </summary>
        public double MeanValue { get; set; }

        /// <summary>
        /// 实际是否反转
        /// </summary>
        public bool InvertUsed { get; set; }

        /// <summary>
        /// 输入图像尺寸
        /// </summary>
        public OpenCvSharp.Size InputSize { get; set; }

        /// <summary>
        /// 获取输出图像
        /// </summary>
        public override Mat? GetOutputImage() => OutputImage;

        /// <summary>
        /// 获取属性映射
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new(nameof(OutputImage),        "输出图像",          null),
                new(nameof(MethodUsed),         "二值化方法",        "结果"),
                new(nameof(ThresholdUsed),      "实际使用的阈值",    "结果"),
                new(nameof(MaxValueUsed),       "实际使用的最大值",  "结果"),
                new(nameof(WhitePixelCount),    "白色像素数",        "结果.统计"),
                new(nameof(WhitePixelRatio),    "白色像素比例",      "结果.统计"),
                new(nameof(RegionCount),        "区域个数",          "结果.统计"),
                new(nameof(MeanValue),          "输出均值",          "结果.统计"),
                new(nameof(InvertUsed),         "是否反转",          "结果"),
                new(nameof(InputSize),          "输入图像尺寸",      "结果"),
                new(nameof(ExecutionTimeMs),    "执行耗时",          "执行信息"),
            ];
        }
    }
}
