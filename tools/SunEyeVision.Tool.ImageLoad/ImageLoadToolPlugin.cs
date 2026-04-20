using System;
using System.Collections.Generic;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入参数
    /// </summary>
    /// <remarks>
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "ImageLoad"。
    /// </remarks>
    [JsonDerivedType(typeof(ImageLoadParameters), "ImageLoad")]
    public class ImageLoadParameters : ToolParameters
    {
        private string _filePath = string.Empty;

        /// <summary>
        /// 图像文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value, "文件路径");
        }

        public override ValidationResult Validate()
        {
            var result = base.Validate();
            if (string.IsNullOrEmpty(FilePath))
            {
                result.AddError("文件路径不能为空");
            }
            return result;
        }
    }

    /// <summary>
    /// 图像载入结果
    /// </summary>
    public class ImageLoadResults : ToolResults
    {
        #region 核心属性（原有）

        public Mat? OutputImage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Channels { get; set; }
        public int Depth { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FileFormat { get; set; } = string.Empty;

        #endregion

        #region 新增：尺寸相关属性

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

        #region 新增：格式相关属性

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
                // OpenCvSharp 深度值：0=CV_8U, 1=CV_8S, 2=CV_16U, 3=CV_16S, 4=CV_32S, 5=CV_32F, 6=CV_64F
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

        #region 新增：像素值相关属性（延迟计算）

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
                // 对于多通道图像，计算所有通道的整体最小/最大值
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
                // 计算所有通道的平均值
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
        /// <remarks>
        /// 创建分组结构，便于在绑定源选择器中展示：
        /// - 图像 → 宽度/高度/通道数...
        /// - 像素值 → 最小值/最大值/平均值...
        /// - 文件 → 路径/大小/格式
        /// - 执行 → 耗时
        /// </remarks>
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

        #endregion
    }

    /// <summary>
    /// 图像载入工具 - 从文件读取图像
    /// </summary>
    [Tool("ImageLoad", "图像载入", Description = "从文件载入图像，作为工作流的图像源", Icon = "📁", Category = "采集", Version = "2.0.0", HasDebugWindow = false)]
    public class ImageLoadTool : IToolPlugin<ImageLoadParameters, ImageLoadResults>
    {
        public bool HasDebugWindow => false;

        public FrameworkElement? CreateDebugControl() => null;

        [Obsolete("使用 CreateDebugControl 替代")]
        public System.Windows.Window? CreateDebugWindow() => null;

        public ImageLoadResults Run(Mat image, ImageLoadParameters parameters)
        {
            var result = new ImageLoadResults
            {
                ToolName = "ImageLoad",
                ToolId = "image_load",
                Timestamp = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 验证参数
                var validationResult = parameters.Validate();
                if (!validationResult.IsValid)
                {
                    result.SetError($"参数验证失败: {string.Join(", ", validationResult.Errors)}");
                    return result;
                }

                // 验证文件路径
                if (string.IsNullOrEmpty(parameters.FilePath))
                {
                    result.SetError("文件路径为空");
                    return result;
                }

                if (!File.Exists(parameters.FilePath))
                {
                    result.SetError($"文件不存在: {parameters.FilePath}");
                    return result;
                }

                // 获取文件信息
                var fileInfo = new FileInfo(parameters.FilePath);
                result.FilePath = parameters.FilePath;
                result.FileSize = fileInfo.Length;
                result.FileFormat = fileInfo.Extension.ToUpperInvariant();

                // 使用 OpenCvSharp 加载图像
                var loadedImage = Cv2.ImRead(parameters.FilePath, ImreadModes.Color);

                if (loadedImage == null || loadedImage.Empty())
                {
                    result.SetError($"无法加载图像: {parameters.FilePath}");
                    return result;
                }

                // 设置结果
                result.OutputImage = loadedImage;
                result.Width = loadedImage.Width;
                result.Height = loadedImage.Height;
                result.Channels = loadedImage.Channels();
                result.Depth = (int)loadedImage.Depth();

                stopwatch.Stop();
                result.SetSuccess(stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.SetError($"加载图像失败: {ex.Message}", ex);
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }
    }
}
