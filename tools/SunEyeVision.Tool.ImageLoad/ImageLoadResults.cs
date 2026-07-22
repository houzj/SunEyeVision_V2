using System;
using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入结果
    /// </summary>
    public class ImageLoadResults : ToolResults
    {
        #region 核心属性

        /// <summary>
        /// 输出图像（原始加载图像）
        /// </summary>
        public IVisionImage? OutputImage { get; set; }

        /// <summary>
        /// 灰度图像（彩色输入时转换得到，灰度输入时为原图克隆）
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

        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
        public int Depth { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileFormat { get; set; } = string.Empty;

        #endregion

        #region 尺寸相关属性

        /// <summary>
        /// 宽高比（宽度/高度）
        /// </summary>
        public double AspectRatio => Height > 0 ? (double)Width / Height : 0.0;

        /// <summary>
        /// 总像素数
        /// </summary>
        public long TotalPixels => (long)Width * Height;

        #endregion

        #region 格式相关属性

        /// <summary>
        /// Halcon 图像类型描述（如 "byte"）
        /// </summary>
        public string ImageTypeDisplay
        {
            get
            {
                if (OutputImage == null || OutputImage.IsEmpty)
                    return "Unknown";
                return Channels switch
                {
                    1 => "byte (灰度)",
                    3 => "byte (彩色)",
                    _ => $"byte ({Channels}通道)"
                };
            }
        }

        /// <summary>
        /// 是否为彩色图像
        /// </summary>
        public bool IsColor => Channels == 3 || Channels == 4;

        /// <summary>
        /// 是否为灰度图像
        /// </summary>
        public bool IsGrayscale => Channels == 1;

        /// <summary>
        /// 颜色类型描述
        /// </summary>
        public string ColorType
        {
            get
            {
                return Channels switch
                {
                    1 => "灰度",
                    3 => "彩色",
                    4 => "彩色",
                    _ => $"{Channels}通道"
                };
            }
        }

        #endregion

        #region 像素值相关属性（延迟计算）

        private double? _minPixelValue;
        private double? _maxPixelValue;
        private double? _meanPixelValue;

        /// <summary>
        /// 最小像素值
        /// </summary>
        public double MinPixelValue
        {
            get
            {
                if (_minPixelValue == null && OutputImage != null && !OutputImage.IsEmpty)
                {
                    CalculatePixelValues();
                }
                return _minPixelValue ?? 0.0;
            }
        }

        /// <summary>
        /// 最大像素值
        /// </summary>
        public double MaxPixelValue
        {
            get
            {
                if (_maxPixelValue == null && OutputImage != null && !OutputImage.IsEmpty)
                {
                    CalculatePixelValues();
                }
                return _maxPixelValue ?? 0.0;
            }
        }

        /// <summary>
        /// 平均像素值
        /// </summary>
        public double MeanPixelValue
        {
            get
            {
                if (_meanPixelValue == null && OutputImage != null && !OutputImage.IsEmpty)
                {
                    CalculatePixelValues();
                }
                return _meanPixelValue ?? 0.0;
            }
        }

        /// <summary>
        /// 像素值范围描述
        /// </summary>
        public string PixelValueRange => $"{MinPixelValue:F1} - {MaxPixelValue:F1}";

        /// <summary>
        /// 计算像素值范围（延迟计算，通过 Halcon MinMaxGray/Intensity）
        /// </summary>
        private void CalculatePixelValues()
        {
            if (OutputImage == null || OutputImage.IsEmpty) return;

            // 拆包获取 HImage 计算像素统计
            if (OutputImage is not HalImage halImage) return;
            var hImage = halImage.Unwrap();
            if (hImage == null || !hImage.IsInitialized()) return;

            HOperatorSet.MinMaxGray(hImage, hImage, 0, out var min, out var max, out _);
            _minPixelValue = min.D;
            _maxPixelValue = max.D;

            HOperatorSet.Intensity(hImage, hImage, out var mean, out _);
            _meanPixelValue = mean.D;
        }

        #endregion

        #region 重写方法

        /// <summary>
        /// 获取属性映射表
        /// </summary>
        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("OutputImage",          "输出图像",     null),
                new("GrayImage",            "灰度图像",     null),
                new("Width",                "宽度",         "图像"),
                new("Height",               "高度",         "图像"),
                new("Channels",             "通道数",       "图像"),
                new("Depth",                "深度",         "图像"),
                new("ImageTypeDisplay",     "类型名称",     "图像"),
                new("IsColor",              "是否彩色",     "图像"),
                new("IsGrayscale",          "是否灰度",     "图像"),
                new("ColorType",            "颜色类型",     "图像"),
                new("AspectRatio",          "宽高比",       "图像"),
                new("TotalPixels",          "总像素数",     "图像"),
                new("MinPixelValue",        "最小值",       "像素值"),
                new("MaxPixelValue",        "最大值",       "像素值"),
                new("MeanPixelValue",       "平均值",       "像素值"),
                new("PixelValueRange",      "范围描述",     "像素值"),
                new("FilePath",             "路径",         "文件"),
                new("FileSize",             "大小",         "文件"),
                new("FileFormat",           "格式",         "文件"),
                new("ExecutionTimeMs",      "耗时",         "执行"),
            ];
        }

        #endregion
    }
}
