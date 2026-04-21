using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.BlobAnalysis.Models
{
    /// <summary>
    /// Blob分析图像显示配置
    /// </summary>
    public class BlobAnalysisDisplayConfig : ObservableObject
    {
        private DisplayItemConfig _binaryImage = new() { Name = "二值图像" };
        /// <summary>
        /// 二值图像显示配置
        /// </summary>
        public DisplayItemConfig BinaryImage
        {
            get => _binaryImage;
            set => SetProperty(ref _binaryImage, value, "二值图像配置");
        }

        private DisplayItemConfig _blobContour = new() { Name = "Blob轮廓" };
        /// <summary>
        /// Blob轮廓显示配置
        /// </summary>
        public DisplayItemConfig BlobContour
        {
            get => _blobContour;
            set => SetProperty(ref _blobContour, value, "Blob轮廓配置");
        }

        private DisplayItemConfig _blobBoundingBox = new() { Name = "外接矩形" };
        /// <summary>
        /// 外接矩形显示配置
        /// </summary>
        public DisplayItemConfig BlobBoundingBox
        {
            get => _blobBoundingBox;
            set => SetProperty(ref _blobBoundingBox, value, "外接矩形配置");
        }

        private DisplayItemConfig _blobCentroid = new() { Name = "质心标记" };
        /// <summary>
        /// 质心标记显示配置
        /// </summary>
        public DisplayItemConfig BlobCentroid
        {
            get => _blobCentroid;
            set => SetProperty(ref _blobCentroid, value, "质心标记配置");
        }

        private DisplayItemConfig _region = new() { Name = "检测区域" };
        /// <summary>
        /// 检测区域显示配置
        /// </summary>
        public DisplayItemConfig Region
        {
            get => _region;
            set => SetProperty(ref _region, value, "检测区域配置");
        }

        private DisplayItemConfig _histogram = new() { Name = "直方图" };
        /// <summary>
        /// 直方图显示配置
        /// </summary>
        public DisplayItemConfig Histogram
        {
            get => _histogram;
            set => SetProperty(ref _histogram, value, "直方图配置");
        }

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public BlobAnalysisDisplayConfig Clone()
        {
            return new BlobAnalysisDisplayConfig
            {
                BinaryImage = _binaryImage.Clone(),
                BlobContour = _blobContour.Clone(),
                BlobBoundingBox = _blobBoundingBox.Clone(),
                BlobCentroid = _blobCentroid.Clone(),
                Region = _region.Clone(),
                Histogram = _histogram.Clone()
            };
        }
    }

    /// <summary>
    /// Blob分析文本显示配置
    /// </summary>
    public class BlobAnalysisTextConfig : ObservableObject
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public BlobAnalysisTextConfig()
        {
            PluginLogger.Info($"BlobAnalysisTextConfig 初始化: OkColor={_okColor}, NgColor={_ngColor}", "BlobAnalysisTool");
        }

        private string _content = "结果: {Result}";
        /// <summary>
        /// 文本内容（支持变量占位符）
        /// </summary>
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value, "文本内容");
        }

        private Color _okColor = Color.FromRgb(0, 255, 0);
        /// <summary>
        /// OK状态文本颜色
        /// </summary>
        public Color OkColor
        {
            get => _okColor;
            set => SetProperty(ref _okColor, value, "OK颜色");
        }

        private Color _ngColor = Color.FromRgb(255, 0, 0);
        /// <summary>
        /// NG状态文本颜色
        /// </summary>
        public Color NgColor
        {
            get => _ngColor;
            set => SetProperty(ref _ngColor, value, "NG颜色");
        }

        private double _fontSize = 14;
        /// <summary>
        /// 字号
        /// </summary>
        public double FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value, "字号");
        }

        private double _opacity = 1.0;
        /// <summary>
        /// 透明度(0-1)
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value, "透明度");
        }

        private double _positionX = 10;
        /// <summary>
        /// X坐标位置
        /// </summary>
        public double PositionX
        {
            get => _positionX;
            set => SetProperty(ref _positionX, value, "X坐标");
        }

        private double _positionY = 10;
        /// <summary>
        /// Y坐标位置
        /// </summary>
        public double PositionY
        {
            get => _positionY;
            set => SetProperty(ref _positionY, value, "Y坐标");
        }

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public BlobAnalysisTextConfig Clone()
        {
            return new BlobAnalysisTextConfig
            {
                Content = _content,
                OkColor = _okColor,
                NgColor = _ngColor,
                FontSize = _fontSize,
                Opacity = _opacity,
                PositionX = _positionX,
                PositionY = _positionY
            };
        }
    }
}
