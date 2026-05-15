using System;
using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.ImageLoad
{
    public class ImageLoadResults : ToolResults
    {
        #region 核心属性

        public HImage? OutputImage { get; set; }

        /// <summary>灰度图像</summary>
        public HImage? GrayImage { get; set; }

        public override Dictionary<string, HImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, HImage?>();
            if (OutputImage != null && OutputImage.IsInitialized())
                images["OutputImage"] = OutputImage;
            if (GrayImage != null && GrayImage.IsInitialized())
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

        public string Size => $"{Width}x{Height}";
        public double AspectRatio => Height > 0 ? (double)Width / Height : 0.0;
        public long TotalPixels => (long)Width * Height;

        #endregion

        #region 格式相关属性

        public string TypeName
        {
            get
            {
                if (OutputImage == null || !OutputImage.IsInitialized())
                    return "Unknown";

                HOperatorSet.GetImageType(OutputImage, out var type);
                int channels = Channels;
                return Depth switch
                {
                    0 => $"byte ({channels} channel(s))",
                    2 => $"uint2 ({channels} channel(s))",
                    4 => $"int4 ({channels} channel(s))",
                    5 => $"real ({channels} channel(s))",
                    _ => $"{type.S} ({channels} channel(s))"
                };
            }
        }

        public string DepthName
        {
            get
            {
                return Depth switch
                {
                    0 => "byte",
                    1 => "int1",
                    2 => "uint2",
                    3 => "int2",
                    4 => "int4",
                    5 => "real",
                    6 => "complex",
                    _ => "unknown"
                };
            }
        }

        public bool IsColor => Channels == 3 || Channels == 4;
        public bool IsGrayscale => Channels == 1;

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

        public double MinPixelValue
        {
            get
            {
                if (_minPixelValue == null && OutputImage != null && OutputImage.IsInitialized())
                    CalculatePixelValueRange();
                return _minPixelValue ?? 0.0;
            }
        }

        public double MaxPixelValue
        {
            get
            {
                if (_maxPixelValue == null && OutputImage != null && OutputImage.IsInitialized())
                    CalculatePixelValueRange();
                return _maxPixelValue ?? 0.0;
            }
        }

        public double MeanPixelValue
        {
            get
            {
                if (_meanPixelValue == null && OutputImage != null && OutputImage.IsInitialized())
                    CalculateMeanPixelValue();
                return _meanPixelValue ?? 0.0;
            }
        }

        public string PixelValueRange => $"{MinPixelValue:F1} - {MaxPixelValue:F1}";

        private void CalculatePixelValueRange()
        {
            if (OutputImage == null || !OutputImage.IsInitialized())
                return;

            try
            {
                HOperatorSet.MinMaxGray(OutputImage, OutputImage, 0, out var min, out var max, out _);
                _minPixelValue = min.D;
                _maxPixelValue = max.D;
            }
            catch
            {
                _minPixelValue = 0.0;
                _maxPixelValue = 255.0;
            }
        }

        private void CalculateMeanPixelValue()
        {
            if (OutputImage == null || !OutputImage.IsInitialized())
                return;

            try
            {
                HOperatorSet.Intensity(OutputImage, OutputImage, out var mean, out _);
                _meanPixelValue = mean.D;
            }
            catch
            {
                _meanPixelValue = 0.0;
            }
        }

        #endregion

        #region 重写方法

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                new("OutputImage",      "输出图像",     null),
                new("GrayImage",        "灰度图像",     null),
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
