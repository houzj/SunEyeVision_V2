using System;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace SunEyeVision.CvEngine.Models
{
    /// <summary>
    /// Blob信息 - 单个连通域的高级特征数据
    /// </summary>
    /// <remarks>
    /// 对应Halcon的region属性：area_center, circularity, rectangularity等
    /// </remarks>
    public class BlobInfo
    {
        /// <summary>
        /// 连通域标签
        /// </summary>
        public int Label { get; set; }

        /// <summary>
        /// 面积（像素数）
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// 周长（像素）
        /// </summary>
        public double Perimeter { get; set; }

        /// <summary>
        /// 质心X坐标
        /// </summary>
        public double CentroidX { get; set; }

        /// <summary>
        /// 质心Y坐标
        /// </summary>
        public double CentroidY { get; set; }

        /// <summary>
        /// 圆度 (0~1, 1=完美圆)
        /// </summary>
        /// <remarks>
        /// 计算公式: 4π × 面积 / 周长²
        /// </remarks>
        public double Circularity { get; set; }

        /// <summary>
        /// 矩形度 (0~1, 1=完美矩形)
        /// </summary>
        /// <remarks>
        /// 计算公式: 面积 / 外接矩形面积
        /// </remarks>
        public double Rectangularity { get; set; }

        /// <summary>
        /// 外接矩形
        /// </summary>
        public Rect BoundingBox { get; set; }

        /// <summary>
        /// 外接矩形文本（用于显示）
        /// </summary>
        public string BoundingRectText => $"{BoundingBox.Width}×{BoundingBox.Height}";

        /// <summary>
        /// 是否通过筛选
        /// </summary>
        public bool IsFiltered { get; set; } = true;

        /// <summary>
        /// 是否通过独立Blob判断（per-blob judgment）
        /// </summary>
        /// <remarks>
        /// 由结果判断中的 per-blob 条件驱动（面积/圆度/矩形度），
        /// 用于控制该 blob 各显示元素的 OK/NG 颜色。
        /// 与工具整体判断（节点状态 IsPass）独立。
        /// </remarks>
        public bool JudgmentPassed { get; set; } = true;

        /// <summary>
        /// 轮廓数据 - 对应 Halcon HXLD
        /// </summary>
        [JsonIgnore]
        public CvContour? Contour { get; set; }

        /// <summary>
        /// 创建BlobInfo的便捷工厂方法
        /// </summary>
        public static BlobInfo Create(
            int label,
            double area,
            double perimeter,
            double centroidX,
            double centroidY,
            Rect boundingBox,
            double circularity = 0,
            double rectangularity = 0,
            CvContour? contour = null)
        {
            return new BlobInfo
            {
                Label = label,
                Area = area,
                Perimeter = perimeter,
                CentroidX = centroidX,
                CentroidY = centroidY,
                Circularity = Math.Min(circularity, 1.0),
                Rectangularity = Math.Min(rectangularity, 1.0),
                BoundingBox = boundingBox,
                Contour = contour
            };
        }
    }
}
