using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 图像阈值化工具执行结果
    /// </summary>
    public class ThresholdResults : ToolResults
    {
        #region 输出图像

        /// <summary>
        /// 输出图像（阈值化结果 ComposeDisplay 合成图）
        /// </summary>
        public IVisionImage? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像中间结果（灰度转换后）
        /// </summary>
        public IVisionImage? GrayImage { get; set; }

        /// <summary>
        /// ReduceDomain 中间结果（ROI限制后的灰度图）
        /// </summary>
        public IVisionImage? ReducedImage { get; set; }

        /// <summary>
        /// 二值掩码图像（CV_8UC1, 0/255 纯二值）
        /// </summary>
        public IVisionImage? BinaryMask { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            if (OutputImage != null && !OutputImage.IsEmpty)
                images["OutputImage"] = OutputImage;
            if (GrayImage != null && !GrayImage.IsEmpty)
                images["GrayImage"] = GrayImage;
            if (ReducedImage != null && !ReducedImage.IsEmpty)
                images["ReducedImage"] = ReducedImage;
            if (BinaryMask != null && !BinaryMask.IsEmpty)
                images["BinaryMask"] = BinaryMask;
            return images;
        }

        #endregion

        #region 参数回写（实际使用的参数值）

        /// <summary>
        /// 实际使用的阈值（Otsu/Triangle 时自动计算得到）
        /// </summary>
        public double ThresholdUsed { get; set; }

        /// <summary>
        /// 实际使用的最大值
        /// </summary>
        public double MaxValueUsed { get; set; }

        /// <summary>
        /// 实际使用的阈值模式
        /// </summary>
        public string TypeUsed { get; set; } = string.Empty;

        /// <summary>
        /// 实际使用的自适应方法
        /// </summary>
        public string AdaptiveMethodUsed { get; set; } = string.Empty;

        /// <summary>
        /// 实际使用的块大小
        /// </summary>
        public int BlockSizeUsed { get; set; }

        #endregion

        #region 输入图像信息

        /// <summary>
        /// 输入图像尺寸
        /// </summary>
        public OpenCvSharp.Size InputSize { get; set; }

        /// <summary>
        /// 输入图像通道数
        /// </summary>
        public int InputChannels { get; set; }

        #endregion

        #region 统计结果

        /// <summary>
        /// 白色像素数
        /// </summary>
        public long WhitePixelCount { get; set; }

        /// <summary>
        /// 白色像素比例(0-100%)
        /// </summary>
        public double WhitePixelRatio { get; set; }

        /// <summary>
        /// 输出图像均值
        /// </summary>
        public double OutputMean { get; set; }

        /// <summary>
        /// 输出面积（=白色像素数）
        /// </summary>
        public long OutputArea { get; set; }

        /// <summary>
        /// 质心X坐标
        /// </summary>
        public double CentroidX { get; set; }

        /// <summary>
        /// 质心Y坐标
        /// </summary>
        public double CentroidY { get; set; }

        #endregion

        #region 图形结果（供下游节点数据绑定用）

        /// <summary>
        /// 二值区域集合（已桥接为 RoI 外发，供下游节点绑定/全局变量复用）
        /// </summary>
        public ShapeParameters[] OutputRegions { get; set; } = Array.Empty<ShapeParameters>();

        #endregion

        #region 分析数据

        /// <summary>
        /// 灰度直方图（256个bin，归一化0-1）
        /// </summary>
        public double[] HistogramData { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 输入图像各通道均值
        /// </summary>
        public double[] ChannelMeans { get; set; } = Array.Empty<double>();

        #endregion

        #region 判断结果

        /// <summary>
        /// 是否通过所有判断条件
        /// </summary>
        public bool IsPass { get; set; } = true;

        /// <summary>
        /// 判断失败原因列表
        /// </summary>
        public List<string> FailReasons { get; set; } = new();

        #endregion

        #region 可视化与属性映射

        /// <summary>内部默认文本样式（Threshold 工具不通过 Parameters 注入 Style）</summary>
        private static readonly TextStyle DefaultTextStyle = new()
        {
            Name = "ThresholdInfo",
            FontSize = 11.0,
            PositionX = 10,
            PositionY = 22
        };

        /// <summary>
        /// 构建显示项（通过 DisplayCollector Push 输出）
        /// </summary>
        public void BuildDisplayItems()
        {
            _display.Clear();
            if (InputSize.Width <= 0 || InputSize.Height <= 0) return;

            var resultText = IsPass ? "OK" : "NG";

            _display.AddText(DefaultTextStyle, DefaultTextStyle.PositionX, DefaultTextStyle.PositionY, new[]
            {
                $"阈值: {ThresholdUsed:F0}  最大值: {MaxValueUsed:F0}",
                $"类型: {TypeUsed}  {resultText}"
            });
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("OutputImage",          "输出图像",               null),
                new("GrayImage",            "灰度图像",               null),
                new("ReducedImage",         "ROI缩减图像",            null),
                new("BinaryMask",           "二值掩码",               null),
                new("ThresholdUsed",        "实际使用的阈值",         "参数回写"),
                new("MaxValueUsed",         "实际使用的最大值",       "参数回写"),
                new("TypeUsed",             "阈值类型",               "参数回写"),
                new("AdaptiveMethodUsed",   "自适应方法",             "参数回写"),
                new("BlockSizeUsed",        "块大小",                 "参数回写"),
                new("OutputRegions",        "二值区域",               "图形结果"),
                new("InputChannels",        "输入图像通道数",         "输入信息"),
                new("WhitePixelCount",      "白色像素数",             "统计"),
                new("WhitePixelRatio",      "白色像素比例(%)",        "统计"),
                new("OutputMean",           "输出图像均值",           "统计"),
                new("OutputArea",           "输出面积",               "统计"),
                new("CentroidX",            "质心X坐标",             "统计"),
                new("CentroidY",            "质心Y坐标",             "统计"),
                new("HistogramData",        "直方图数据",             "分析"),
                new("ChannelMeans",         "通道均值",               "分析"),
                new("IsPass",               "是否通过",               "判断"),
                new("FailReasons",          "失败原因",               "判断"),
            ];
        }

        #endregion
    }
}
