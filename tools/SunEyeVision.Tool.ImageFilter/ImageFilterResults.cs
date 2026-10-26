using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 图像滤波工具执行结果
    /// </summary>
    // P0.6-1 能力声明：图像输入 + 位置修正 + 区域输入
    [ToolCapability(ToolCapabilities.ImageSource | ToolCapabilities.PositionCorrection
        | ToolCapabilities.RoiInput)]
    public class ImageFilterResults : ToolResults
    {
        public ImageFilterResults()=>Resources=new ResultResourceOwner(ReleaseOwnedImages);
        private void ReleaseOwnedImages()
        {
            var failures=new List<Exception>();
            foreach(var image in new IDisposable?[]{OutputImage,GrayImage}.Where(image=>image is not null).Distinct(ReferenceEqualityComparer.Instance).Cast<IDisposable>())
                try{image.Dispose();}catch(Exception error){failures.Add(error);}
            if(failures.Count>0)throw new AggregateException("ImageFilter输出图像释放失败。",failures);
        }

        #region 输出图像

        /// <summary>
        /// 输出图像（滤波结果）
        /// </summary>
        public IVisionImage? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像中间结果
        /// </summary>
        public IVisionImage? GrayImage { get; set; }

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
            return images;
        }

        #endregion

        #region 参数回写（实际使用的参数值）

        /// <summary>
        /// 实际使用的滤波类型
        /// </summary>
        public string FilterTypeUsed { get; set; } = string.Empty;

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
        /// 判断失败原因列表
        /// </summary>
        public List<string> FailReasons { get; set; } = new();

        #endregion

        #region 可视化与属性映射

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                new("OutputImage",          "输出图像",               null),
                new("GrayImage",            "灰度图像",               null),
                new("FilterTypeUsed",       "滤波类型",               "参数回写"),
                new("KernelWidthUsed",      "内核宽度",               "参数回写"),
                new("KernelHeightUsed",     "内核高度",               "参数回写"),
                new("InputChannels",        "输入图像通道数",         "输入信息"),
                new("OutputMean",           "输出均值",               "统计"),
                new("OutputMin",            "输出最小值",             "统计"),
                new("OutputMax",            "输出最大值",             "统计"),
                new("OutputStd",            "输出标准差",             "统计"),
                new("ChannelMeans",         "通道均值",               "分析"),
                new("FailReasons",          "失败原因",               "判断"),
            ];
        }

        #endregion
    }
}
