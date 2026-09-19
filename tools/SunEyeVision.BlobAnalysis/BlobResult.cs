using System;
using System.Windows;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// Blob 分析结果数据 - Halcon 原生，零 OpenCV 依赖
    /// </summary>
    public class BlobResult
    {
        /// <summary>连通域序号（筛选后从1开始）</summary>
        public int Label { get; set; }

        /// <summary>面积（像素数）</summary>
        public double Area { get; set; }

        /// <summary>周长（像素）</summary>
        public double Perimeter { get; set; }

        /// <summary>质心X坐标</summary>
        public double CentroidX { get; set; }

        /// <summary>质心Y坐标</summary>
        public double CentroidY { get; set; }

        /// <summary>圆度 (0~1, 1=完美圆)</summary>
        public double Circularity { get; set; }

        /// <summary>矩形度 (0~1, 1=完美矩形)</summary>
        public double Rectangularity { get; set; }

        /// <summary>外接矩形（WPF坐标系）</summary>
        public Rect BoundingBox { get; set; }

        /// <summary>最小外接旋转矩形四角点（WPF坐标系）</summary>
        public WpfPoint[]? RotatedBoundingBox { get; set; }

        /// <summary>外接矩形文本</summary>
        public string BoundingRectText => $"{(int)BoundingBox.Width}×{(int)BoundingBox.Height}";

        /// <summary>是否通过统一判断</summary>
        public bool JudgmentPassed { get; set; } = true;

        /// <summary>轮廓点集（WPF坐标系，供显示直接使用）</summary>
        public WpfPoint[]? Contour { get; set; }

        /// <summary>是否有轮廓数据</summary>
        public bool HasContour => Contour != null && Contour.Length > 0;
    }
}
