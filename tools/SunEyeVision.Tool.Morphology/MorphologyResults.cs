using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Morphology
{
    /// <summary>
    /// 形态学处理工具执行结果
    /// </summary>
    public class MorphologyResults : ToolResults
    {
        #region 输出图像

        /// <summary>
        /// 输出图像（形态学处理后的 ComposeDisplay 合成图）
        /// </summary>
        public IVisionImage? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像中间结果
        /// </summary>
        public IVisionImage? GrayImage { get; set; }

        /// <summary>
        /// 二值掩码（CV_8UC1, 0/255）
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
            if (BinaryMask != null && !BinaryMask.IsEmpty)
                images["BinaryMask"] = BinaryMask;
            return images;
        }

        #endregion

        #region 参数回写

        /// <summary>
        /// 实际使用的形态学操作
        /// </summary>
        public string OperationUsed { get; set; } = string.Empty;

        /// <summary>
        /// 实际使用的结构元素形状
        /// </summary>
        public string KernelShapeUsed { get; set; } = string.Empty;

        /// <summary>
        /// 实际使用的核宽度
        /// </summary>
        public int KernelWidthUsed { get; set; }

        /// <summary>
        /// 实际使用的核高度
        /// </summary>
        public int KernelHeightUsed { get; set; }

        /// <summary>
        /// 实际使用的迭代次数
        /// </summary>
        public int IterationsUsed { get; set; }

        #endregion

        #region 输入图像信息

        /// <summary>
        /// 输入图像宽度
        /// </summary>
        public int InputWidth { get; set; }

        /// <summary>
        /// 输入图像高度
        /// </summary>
        public int InputHeight { get; set; }

        /// <summary>
        /// 输入图像通道数
        /// </summary>
        public int InputChannels { get; set; }

        #endregion

        #region 统计结果

        /// <summary>
        /// 输出图像均值
        /// </summary>
        public double OutputMean { get; set; }

        /// <summary>
        /// Blob 数量（连通域个数）
        /// </summary>
        public int BlobCount { get; set; }

        /// <summary>
        /// 总面积（白色像素数）
        /// </summary>
        public long TotalArea { get; set; }

        /// <summary>
        /// 最大 Blob 面积
        /// </summary>
        public double MaxBlobArea { get; set; }

        #endregion

        #region 分析数据

        /// <summary>
        /// 各通道均值
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

        /// <summary>内部默认文本样式</summary>
        private static readonly TextStyle DefaultTextStyle = new()
        {
            Name = "MorphologyInfo",
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
            if (InputWidth <= 0 || InputHeight <= 0) return;

            var resultText = IsPass ? "OK" : "NG";

            _display.AddText(DefaultTextStyle, DefaultTextStyle.PositionX, DefaultTextStyle.PositionY, new[]
            {
                $"操作: {OperationUsed}  核: {KernelWidthUsed}x{KernelHeightUsed}",
                $"Blob数: {BlobCount}  总面积: {TotalArea}  {resultText}"
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
                new("BinaryMask",           "二值掩码",               null),
                new("OperationUsed",        "实际使用的操作",         "参数回写"),
                new("KernelShapeUsed",      "结构元素形状",           "参数回写"),
                new("KernelWidthUsed",      "核宽度",                 "参数回写"),
                new("KernelHeightUsed",     "核高度",                 "参数回写"),
                new("IterationsUsed",       "迭代次数",               "参数回写"),
                new("InputChannels",        "输入图像通道数",         "输入信息"),
                new("OutputMean",           "输出图像均值",           "统计"),
                new("BlobCount",            "Blob数量",               "统计"),
                new("TotalArea",            "总面积",                 "统计"),
                new("MaxBlobArea",          "最大Blob面积",           "统计"),
                new("ChannelMeans",         "通道均值",               "分析"),
                new("IsPass",               "是否通过",               "判断"),
                new("FailReasons",          "失败原因",               "判断"),
            ];
        }

        #endregion
    }
}
