using System;
using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 图像二值化工具执行结果
    /// </summary>
    public class ImageThresholdResults : ToolResults
    {
        #region 输出图像

        /// <summary>
        /// 输出图像（二值化结果 ComposeDisplay 合成图）
        /// </summary>
        public Mat? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像中间结果
        /// </summary>
        public Mat? GrayImage { get; set; }

        /// <summary>
        /// 二值掩码图像（CV_8UC1, 0/255 纯二值）
        /// </summary>
        public Mat? BinaryMask { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            if (OutputImage != null && !OutputImage.Empty())
                images["OutputImage"] = VisionImage.FromMat(OutputImage);
            if (GrayImage != null && !GrayImage.Empty())
                images["GrayImage"] = VisionImage.FromMat(GrayImage);
            if (BinaryMask != null && !BinaryMask.Empty())
                images["BinaryMask"] = VisionImage.FromMat(BinaryMask);
            return images;
        }

        #endregion

        #region 参数回写

        /// <summary>
        /// 实际使用的阈值（Otsu/Triangle 时自动计算）
        /// </summary>
        public double ThresholdUsed { get; set; }

        /// <summary>
        /// 实际使用的最大值
        /// </summary>
        public int MaxValueUsed { get; set; }

        /// <summary>
        /// 实际使用的阈值方法
        /// </summary>
        public ThresholdMethod MethodUsed { get; set; }

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

        #endregion

        #region 图形结果（供下游节点数据绑定用）

        /// <summary>
        /// 二值区域集合（含1个区域，保持集合格式统一）
        /// </summary>
        public IVisionRegion[] OutputRegions { get; set; } = Array.Empty<IVisionRegion>();

        #endregion

        #region 分析数据

        /// <summary>
        /// 灰度直方图（256个bin，归一化0-1）
        /// </summary>
        public double[] HistogramData { get; set; } = Array.Empty<double>();

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

        private static readonly TextStyle DefaultTextStyle = new()
        {
            Name = "ImageThresholdInfo",
            FontSize = 11.0,
            PositionX = 10,
            PositionY = 22
        };

        /// <summary>
        /// 构建显示项
        /// </summary>
        public void BuildDisplayItems()
        {
            _display.Clear();
            if (InputSize.Width <= 0 || InputSize.Height <= 0) return;

            var resultText = IsPass ? "OK" : "NG";
            _display.AddText(DefaultTextStyle, DefaultTextStyle.PositionX, DefaultTextStyle.PositionY, new[]
            {
                $"方法: {MethodUsed}  阈值: {ThresholdUsed:F0}",
                $"白色比例: {WhitePixelRatio:F1}%  {resultText}"
            });
        }

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("OutputImage",     "输出图像",          null),
                new("GrayImage",       "灰度图像",          null),
                new("BinaryMask",      "二值掩码",          null),
                new("ThresholdUsed",   "实际使用的阈值",    "参数回写"),
                new("MaxValueUsed",    "实际使用的最大值",  "参数回写"),
                new("MethodUsed",      "阈值方法",          "参数回写"),
                new("OutputRegions",   "二值区域",          "图形结果"),
                new("InputSize",       "输入图像尺寸",      "输入信息"),
                new("InputChannels",   "输入图像通道数",    "输入信息"),
                new("WhitePixelCount", "白色像素数",        "统计"),
                new("WhitePixelRatio", "白色像素比例(%)",   "统计"),
                new("OutputMean",      "输出图像均值",      "统计"),
                new("OutputArea",      "输出面积",          "统计"),
                new("HistogramData",   "直方图数据",        "分析"),
                new("IsPass",          "是否通过",          "判断"),
                new("FailReasons",     "失败原因",          "判断"),
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
            dict["MethodUsed"] = MethodUsed.ToString();
            dict["InputChannels"] = InputChannels;
            dict["WhitePixelCount"] = WhitePixelCount;
            dict["WhitePixelRatio"] = WhitePixelRatio;
            dict["OutputMean"] = OutputMean;
            dict["OutputArea"] = OutputArea;
            dict["IsPass"] = IsPass;
            if (FailReasons.Count > 0)
                dict["FailReasons"] = string.Join("; ", FailReasons);
            return dict;
        }

        #endregion
    }
}
