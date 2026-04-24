using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// OpenCV区域封装类 - 对应Halcon的HRegion
    /// </summary>
    /// <remarks>
    /// 算子对照表：
    /// | Halcon算子           | 本类方法           | 说明                     |
    /// |----------------------|--------------------|--------------------------|
    /// | connection           | Connection         | 连通域分析               |
    /// | select_shape         | SelectShape        | 形状筛选                 |
    /// | area_center          | (内嵌于Connection) | 面积和中心坐标           |
    /// | union1               | Union1             | 区域并集                 |
    /// 
    /// 设计原则：
    /// 1. 每个公开方法对应一个Halcon算子，命名和语义对齐
    /// 2. 使用using语句管理Mat生命周期，避免资源泄漏
    /// 3. 不依赖SDK，纯算法封装
    /// </remarks>
    public class CvRegion : IDisposable
    {
        #region 私有字段

        private Mat _regionImage;
        private bool _ownsImage;

        #endregion

        #region 构造函数与初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="regionImage">纯二值区域图（CV_8UC1）</param>
        /// <param name="ownsImage">是否拥有图像所有权</param>
        public CvRegion(Mat regionImage, bool ownsImage = false)
        {
            _regionImage = regionImage;
            _ownsImage = ownsImage;
        }

        /// <summary>
        /// 从已有区域创建（克隆副本）
        /// </summary>
        public static CvRegion FromRegion(Mat regionImage)
        {
            return new CvRegion(regionImage.Clone(), ownsImage: true);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取底层Mat区域图像
        /// </summary>
        public Mat RegionImage => _regionImage;

        /// <summary>
        /// 区域尺寸
        /// </summary>
        public Size Size => _regionImage.Size();

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool Empty => _regionImage == null || _regionImage.Empty();

        #endregion

        #region connection - 连通域分析

        /// <summary>
        /// 连通域分析 - 等价于Halcon的connection
        /// </summary>
        /// <param name="connectivity">连通性类型</param>
        /// <returns>所有检测到的Blob列表</returns>
        /// <remarks>
        /// Halcon等价:
        ///   connection (Region, ConnectedRegions)
        ///   area_center (ConnectedRegions, Area, Row, Column)
        /// </remarks>
        public List<Models.BlobInfo> Connection(CvConnectivity connectivity = CvConnectivity.Connect8)
        {
            var blobs = new List<Models.BlobInfo>();

            if (_regionImage == null || _regionImage.Empty())
                return blobs;

            var pixelConnectivity = CvConvert.ToOpenCvConnectivity(connectivity);

            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();

            int numLabels = Cv2.ConnectedComponentsWithStats(
                _regionImage, labels, stats, centroids, pixelConnectivity);

            for (int i = 1; i < numLabels; i++) // 跳过背景(0)
            {
                double area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                double centroidX = centroids.At<double>(i, 0);
                double centroidY = centroids.At<double>(i, 1);

                var bbox = new Rect(
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Left),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Top),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Width),
                    stats.At<int>(i, (int)ConnectedComponentsTypes.Height));

                // 计算形状特征
                var (perimeter, circularity, rectangularity) = ComputeShapeFeatures(labels, i, area, bbox);

                blobs.Add(Models.BlobInfo.Create(
                    label: i,
                    area: area,
                    perimeter: perimeter,
                    centroidX: centroidX,
                    centroidY: centroidY,
                    boundingBox: bbox,
                    circularity: circularity,
                    rectangularity: rectangularity));
            }

            return blobs;
        }

        #endregion

        #region select_shape - 形状筛选

        /// <summary>
        /// 形状筛选 - 等价于Halcon的select_shape
        /// </summary>
        /// <param name="allBlobs">所有检测到的Blob</param>
        /// <param name="minArea">最小面积</param>
        /// <param name="maxArea">最大面积</param>
        /// <param name="minCircularity">最小圆度</param>
        /// <param name="maxCircularity">最大圆度</param>
        /// <param name="minRectangularity">最小矩形度</param>
        /// <param name="maxRectangularity">最大矩形度</param>
        /// <returns>筛选后的Blob列表</returns>
        /// <remarks>
        /// Halcon等价:
        ///   select_shape (ConnectedRegions, Selected, 'area', 'and', Min, Max)
        ///   select_shape (Selected, Final, 'circularity', 'and', 0.4, 1.0)
        /// </remarks>
        public List<Models.BlobInfo> SelectShape(
            List<Models.BlobInfo> allBlobs,
            int minArea,
            int maxArea,
            double minCircularity = 0,
            double maxCircularity = 1,
            double minRectangularity = 0,
            double maxRectangularity = 1)
        {
            var filtered = allBlobs.Where(b =>
                b.Area >= minArea && b.Area <= maxArea &&
                b.Circularity >= minCircularity && b.Circularity <= maxCircularity &&
                b.Rectangularity >= minRectangularity && b.Rectangularity <= maxRectangularity
            ).ToList();

            // 标记每个Blob是否通过筛选
            var allFilteredIds = filtered.Select(b => b.Label).ToHashSet();
            foreach (var blob in allBlobs)
            {
                blob.IsFiltered = allFilteredIds.Contains(blob.Label);
            }

            return filtered;
        }

        #endregion

        #region union1 - 区域并集

        /// <summary>
        /// 区域并集 - 等价于Halcon的union1
        /// </summary>
        /// <param name="blobs">Blob列表</param>
        /// <returns>合并后的区域图像</returns>
        public Mat Union1(List<Models.BlobInfo> blobs)
        {
            if (blobs == null || blobs.Count == 0)
            {
                return new Mat(_regionImage.Size(), MatType.CV_8UC1, Scalar.Black);
            }

            var result = new Mat(_regionImage.Size(), MatType.CV_8UC1, Scalar.Black);

            foreach (var blob in blobs)
            {
                var bbox = blob.BoundingBox;
                if (bbox.Width > 0 && bbox.Height > 0)
                {
                    Cv2.Rectangle(result, bbox, Scalar.White, -1);
                }
            }

            return result;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 计算Blob的形状特征
        /// </summary>
        private static (double perimeter, double circularity, double rectangularity) ComputeShapeFeatures(
            Mat labels, int label, double area, Rect bbox)
        {
            double perimeter = 0;
            double circularity = 0;
            double rectangularity = 0;

            using var blobMask = new Mat();
            Cv2.Compare(labels, label, blobMask, CmpType.EQ);
            Cv2.FindContours(blobMask, out var contours, out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours.Length > 0)
            {
                var contour = contours[0];
                perimeter = Cv2.ArcLength(contour, true);

                // 圆度 = 4π × 面积 / 周长²
                circularity = perimeter > 0
                    ? (4 * Math.PI * area) / (perimeter * perimeter)
                    : 0;
            }

            // 矩形度 = 面积 / 外接矩形面积
            double bboxArea = bbox.Width * bbox.Height;
            rectangularity = bboxArea > 0 ? area / bboxArea : 0;

            return (perimeter, circularity, rectangularity);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_ownsImage && _regionImage != null)
            {
                _regionImage.Dispose();
            }
            _regionImage = null!;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
