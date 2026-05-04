using System;
using System.Collections.Generic;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入结果
    /// </summary>
    public class ImageLoadResults : ToolResults
    {
        #region 核心属性

        public Mat? OutputImage { get; set; }

        /// <summary>
        /// 获取输出图像（DispImage通道）
        /// </summary>
        public override Mat? GetOutputImage() => OutputImage;

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
        /// 图像尺寸（OpenCV标准类型）
        /// </summary>
        public OpenCvSharp.Size Size => new OpenCvSharp.Size(Width, Height);

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
        /// OpenCV类型名称（如"CV_8UC3"）
        /// </summary>
        public string TypeName
        {
            get
            {
                if (OutputImage == null || OutputImage.Empty())
                    return "Unknown";

                return OutputImage.Type().ToString();
            }
        }

        /// <summary>
        /// 深度类型名称（如"CV_8U"）
        /// </summary>
        public string DepthName
        {
            get
            {
                if (OutputImage == null || OutputImage.Empty())
                    return "Unknown";

                var depth = OutputImage.Depth();
                return depth switch
                {
                    0 => "CV_8U",
                    1 => "CV_8S",
                    2 => "CV_16U",
                    3 => "CV_16S",
                    4 => "CV_32S",
                    5 => "CV_32F",
                    6 => "CV_64F",
                    _ => $"Unknown({depth})"
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
                if (_minPixelValue == null && OutputImage != null && !OutputImage.Empty())
                {
                    CalculatePixelValueRange();
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
                if (_maxPixelValue == null && OutputImage != null && !OutputImage.Empty())
                {
                    CalculatePixelValueRange();
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
                if (_meanPixelValue == null && OutputImage != null && !OutputImage.Empty())
                {
                    CalculateMeanPixelValue();
                }
                return _meanPixelValue ?? 0.0;
            }
        }

        /// <summary>
        /// 像素值范围描述
        /// </summary>
        public string PixelValueRange => $"{MinPixelValue:F1} - {MaxPixelValue:F1}";

        /// <summary>
        /// 计算像素值范围（延迟计算）
        /// </summary>
        private void CalculatePixelValueRange()
        {
            if (OutputImage == null || OutputImage.Empty())
                return;

            try
            {
                OutputImage.MinMaxLoc(out double minVal, out double maxVal);
                _minPixelValue = minVal;
                _maxPixelValue = maxVal;
            }
            catch
            {
                _minPixelValue = 0.0;
                _maxPixelValue = 255.0;
            }
        }

        /// <summary>
        /// 计算平均像素值（延迟计算）
        /// </summary>
        private void CalculateMeanPixelValue()
        {
            if (OutputImage == null || OutputImage.Empty())
                return;

            try
            {
                var mean = OutputImage.Mean();
                _meanPixelValue = mean.Val0;
            }
            catch
            {
                _meanPixelValue = 0.0;
            }
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
                new("OutputImage",      "输出图像",     null),
                new("Width",            "宽度",         "图像"),
                new("Height",           "高度",         "图像"),
                new("Size",             "尺寸",         "图像"),
                new("Channels",         "通道数",       "图像"),
                new("Depth",            "深度",         "图像"),
                new("TypeName",         "类型名称",     "图像"),
                new("DepthName",        "深度名称",     "图像"),
                new("IsColor",          "是否彩色",     "图像"),
                new("IsGrayscale",      "是否灰度",     "图像"),
                new("ColorType",        "颜色类型",     "图像"),
                new("AspectRatio",      "宽高比",       "图像"),
                new("TotalPixels",      "总像素数",     "图像"),
                new("MinPixelValue",    "最小值",       "像素值"),
                new("MaxPixelValue",    "最大值",       "像素值"),
                new("MeanPixelValue",   "平均值",       "像素值"),
                new("PixelValueRange",  "范围描述",     "像素值"),
                new("FilePath",         "路径",         "文件"),
                new("FileSize",         "大小",         "文件"),
                new("FileFormat",       "格式",         "文件"),
                new("ExecutionTimeMs",  "耗时",         "执行"),
            ];
        }

        /// <summary>
        /// 获取结果项列表
        /// </summary>
        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>();

            items.Add(ResultItem.Numeric("宽度", Width, "像素"));
            items.Add(ResultItem.Numeric("高度", Height, "像素"));
            items.Add(ResultItem.Numeric("通道数", Channels, "通道"));
            items.Add(ResultItem.Numeric("深度", Depth));
            items.Add(ResultItem.Text("类型名称", TypeName));
            items.Add(ResultItem.Text("深度名称", DepthName));
            items.Add(ResultItem.Boolean("是否彩色", IsColor));
            items.Add(ResultItem.Boolean("是否灰度", IsGrayscale));
            items.Add(ResultItem.Text("颜色类型", ColorType));
            items.Add(ResultItem.Numeric("宽高比", AspectRatio));
            items.Add(ResultItem.Numeric("总像素数", TotalPixels, "像素"));

            items.Add(ResultItem.Numeric("最小值", MinPixelValue));
            items.Add(ResultItem.Numeric("最大值", MaxPixelValue));
            items.Add(ResultItem.Numeric("平均值", MeanPixelValue));
            items.Add(ResultItem.Text("范围描述", PixelValueRange));

            items.Add(ResultItem.Text("路径", FilePath));
            items.Add(ResultItem.Numeric("大小", FileSize, "字节"));
            items.Add(ResultItem.Text("格式", FileFormat));

            items.Add(ResultItem.Numeric("耗时", ExecutionTimeMs, "ms"));

            return items;
        }

        /// <summary>
        /// 转换为字典（补充 ToolResults 基类的字典）
        /// </summary>
        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            dict["Width"] = Width;
            dict["Height"] = Height;
            dict["Channels"] = Channels;
            dict["Depth"] = Depth;
            dict["FilePath"] = FilePath;
            dict["FileSize"] = FileSize;
            dict["FileFormat"] = FileFormat;
            dict["IsColor"] = IsColor;
            dict["IsGrayscale"] = IsGrayscale;
            dict["ColorType"] = ColorType;
            dict["AspectRatio"] = AspectRatio;
            dict["TotalPixels"] = TotalPixels;
            dict["MinPixelValue"] = MinPixelValue;
            dict["MaxPixelValue"] = MaxPixelValue;
            dict["MeanPixelValue"] = MeanPixelValue;
            return dict;
        }

        #endregion
    }
}
