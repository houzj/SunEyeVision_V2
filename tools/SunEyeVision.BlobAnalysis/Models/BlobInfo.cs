using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.BlobAnalysis.Models
{
    /// <summary>
    /// Blob 信息 - 单个连通域的高级特征数据
    /// </summary>
    public class BlobInfo : ObservableObject
    {
        private int _label;
        /// <summary>连通域标签</summary>
        public int Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private double _area;
        /// <summary>面积（像素数）</summary>
        public double Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        private double _perimeter;
        /// <summary>周长（像素）</summary>
        public double Perimeter
        {
            get => _perimeter;
            set => SetProperty(ref _perimeter, value);
        }

        private double _centroidX;
        /// <summary>质心X坐标</summary>
        public double CentroidX
        {
            get => _centroidX;
            set => SetProperty(ref _centroidX, value);
        }

        private double _centroidY;
        /// <summary>质心Y坐标</summary>
        public double CentroidY
        {
            get => _centroidY;
            set => SetProperty(ref _centroidY, value);
        }

        private double _circularity;
        /// <summary>圆度 (0~1, 1=完美圆)</summary>
        public double Circularity
        {
            get => _circularity;
            set => SetProperty(ref _circularity, value);
        }

        private double _rectangularity;
        /// <summary>矩形度 (0~1, 1=完美矩形)</summary>
        public double Rectangularity
        {
            get => _rectangularity;
            set => SetProperty(ref _rectangularity, value);
        }

        /// <summary>外接矩形</summary>
        public OpenCvSharp.Rect BoundingBox { get; set; }

        /// <summary>外接矩形文本（用于DataGrid显示）</summary>
        public string BoundingRectText => $"{BoundingBox.Width}×{BoundingBox.Height}";

        private bool _isFiltered = true;
        /// <summary>是否通过筛选</summary>
        public bool IsFiltered
        {
            get => _isFiltered;
            set => SetProperty(ref _isFiltered, value);
        }
    }
}
