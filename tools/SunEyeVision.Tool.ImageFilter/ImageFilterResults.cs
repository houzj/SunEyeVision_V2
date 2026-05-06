using System;
using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 图像滤波工具执行结果
    /// </summary>
    public class ImageFilterResults : ToolResults
    {
        #region 输出图像

        /// <summary>
        /// 输出图像（滤波结果）
        /// </summary>
        public Mat? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像中间结果
        /// </summary>
        public Mat? GrayImage { get; set; }

        /// <summary>
        /// 获取所有可显示的图像
        /// </summary>
        public override Dictionary<string, Mat?> GetDisplayImages()
        {
            var images = new Dictionary<string, Mat?>();
            if (OutputImage != null && !OutputImage.Empty())
                images["OutputImage"] = OutputImage;
            if (GrayImage != null && !GrayImage.Empty())
                images["GrayImage"] = GrayImage;
            return images;
        }

        #endregion

        #region 参数回写（实际使用的参数值）

        /// <summary>
        /// 实际使用的滤波类型
        /// </summary>
        public FilterType FilterTypeUsed { get; set; }

        /// <summary>
        /// 实际使用的内核宽度
        /// </summary>
        public int KernelWidthUsed { get; set; }

        /// <summary>
        /// 实际使用的内核高度
        /// </summary>
        public int KernelHeightUsed { get; set; }

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
        /// 输出图像均值
        /// </summary>
        public double OutputMean { get; set; }

        /// <summary>
        /// 输出图像最小值
        /// </summary>
        public double OutputMin { get; set; }

        /// <summary>
        /// 输出图像最大值
        /// </summary>
        public double OutputMax { get; set; }

        /// <summary>
        /// 输出图像标准差
        /// </summary>
        public double OutputStd { get; set; }

        #endregion

        #region 分析数据

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

        /// <summary>内部默认文本样式</summary>
        private static readonly TextStyle DefaultTextStyle = new()
        {
            Name = "ImageFilterInfo",
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
                $"滤波: {FilterTypeUsed}  内核: {KernelWidthUsed}x{KernelHeightUsed}",
                $"均值: {OutputMean:F1}  结果: {resultText}"
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
                new("FilterTypeUsed",       "滤波类型",               "参数回写"),
                new("KernelWidthUsed",      "内核宽度",               "参数回写"),
                new("KernelHeightUsed",     "内核高度",               "参数回写"),
                new("InputSize",            "输入图像尺寸",           "输入信息"),
                new("InputChannels",        "输入图像通道数",         "输入信息"),
                new("OutputMean",           "输出均值",               "统计"),
                new("OutputMin",            "输出最小值",             "统计"),
                new("OutputMax",            "输出最大值",             "统计"),
                new("OutputStd",            "输出标准差",             "统计"),
                new("ChannelMeans",         "通道均值",               "分析"),
                new("IsPass",               "是否通过",               "判断"),
                new("FailReasons",          "失败原因",               "判断"),
            ];
        }

        /// <summary>
        /// 转换为字典
        /// </summary>
        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["FilterTypeUsed"] = FilterTypeUsed.ToString();
            dict["KernelWidthUsed"] = KernelWidthUsed;
            dict["KernelHeightUsed"] = KernelHeightUsed;
            dict["InputChannels"] = InputChannels;
            dict["OutputMean"] = OutputMean;
            dict["OutputMin"] = OutputMin;
            dict["OutputMax"] = OutputMax;
            dict["OutputStd"] = OutputStd;
            dict["IsPass"] = IsPass;
            if (FailReasons.Count > 0)
                dict["FailReasons"] = string.Join("; ", FailReasons);
            return dict;
        }

        #endregion
    }
}
