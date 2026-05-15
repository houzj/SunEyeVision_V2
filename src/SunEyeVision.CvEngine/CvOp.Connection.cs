using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
        #region 🔬 连通域与特征查询

        /// <summary>
        /// 连通域分析 - 对应 Halcon connection(Region : : : ConnectedRegions)
        /// </summary>
        public static void Connection(CvRegion region, out CvRegion connectedRegions, CvConnectivity connectivity = CvConnectivity.Connect8)
        {
            var mask = region.Mask;
            if (mask == null || mask.Empty())
            {
                connectedRegions = CvRegion.GenEmptyObj();
                return;
            }

            var pixelConnectivity = CvConvert.ToOpenCvConnectivity(connectivity);

            var labels = new Mat();
            var stats = new Mat();
            var centroids = new Mat();

            int numLabels = Cv2.ConnectedComponentsWithStats(
                mask, labels, stats, centroids, pixelConnectivity);

            if (numLabels <= 1)
            {
                labels.Dispose();
                stats.Dispose();
                centroids.Dispose();
                connectedRegions = CvRegion.GenEmptyObj();
                return;
            }

            var items = new List<RegionItem>(numLabels - 1);
            for (int i = 1; i < numLabels; i++)
                items.Add(new RegionItem(labels, i, region.ImageSize));

            connectedRegions = new CvRegion(items, region.ImageSize, labels, stats, centroids);
        }

        /// <summary>
        /// 面积和质心查询 - 对应 Halcon area_center(Regions : : : Area, Row, Column)
        /// </summary>
        public static void AreaCenter(CvRegion region, out (double Area, double Row, double Column)[] areaCenters)
        {
            if (region.IsEmpty)
            {
                areaCenters = Array.Empty<(double, double, double)>();
                return;
            }

            if (region.Stats != null && !region.Stats.Empty()
                && region.Centroids != null && !region.Centroids.Empty())
            {
                var stats = region.Stats;
                var centroids = region.Centroids;
                int n = stats.Rows;
                areaCenters = new (double, double, double)[n - 1];
                for (int i = 1; i < n; i++)
                    areaCenters[i - 1] = (
                        stats.At<int>(i, (int)ConnectedComponentsTypes.Area),
                        centroids.At<double>(i, 1),
                        centroids.At<double>(i, 0));
                return;
            }

            var mask = region.Mask;
            if (mask == null || mask.Empty())
            {
                areaCenters = Array.Empty<(double, double, double)>();
                return;
            }

            using var labels = new Mat();
            using var statsSlow = new Mat();
            using var centroidsSlow = new Mat();
            int numLabels = Cv2.ConnectedComponentsWithStats(
                mask, labels, statsSlow, centroidsSlow, PixelConnectivity.Connectivity8);

            if (numLabels <= 1)
            {
                areaCenters = Array.Empty<(double, double, double)>();
                return;
            }

            areaCenters = new (double Area, double Row, double Column)[numLabels - 1];
            for (int i = 1; i < numLabels; i++)
                areaCenters[i - 1] = (
                    statsSlow.At<int>(i, (int)ConnectedComponentsTypes.Area),
                    centroidsSlow.At<double>(i, 1),
                    centroidsSlow.At<double>(i, 0));
        }

        /// <summary>
        /// 形状筛选 - 对应 Halcon select_shape(Regions : Selected : Feature, Min, Max)
        /// </summary>
        public static void SelectShape(
            CvRegion region, out CvRegion selected,
            int minArea, int maxArea,
            double minCircularity = 0, double maxCircularity = 1,
            double minRectangularity = 0, double maxRectangularity = 1)
        {
            var mask = region.Mask;
            if (mask == null || mask.Empty())
            {
                selected = CvRegion.GenEmptyObj();
                return;
            }

            bool hasCached = region.HasLabels && region.Stats != null && !region.Stats.Empty();

            Mat labels;
            Mat stats;
            int numLabels;
            bool ownsTempMats = false;

            if (hasCached)
            {
                labels = region.Labels!;
                stats = region.Stats!;
                numLabels = stats.Rows;
            }
            else
            {
                var tmpLabels = new Mat();
                var tmpStats = new Mat();
                using var tmpCentroids = new Mat();
                numLabels = Cv2.ConnectedComponentsWithStats(
                    mask, tmpLabels, tmpStats, tmpCentroids, PixelConnectivity.Connectivity8);
                labels = tmpLabels;
                stats = tmpStats;
                ownsTempMats = true;

                if (numLabels <= 1)
                {
                    double totalArea = Cv2.CountNonZero(mask);
                    if (totalArea >= minArea && totalArea <= maxArea)
                        selected = new CvRegion(mask.Clone(), region.ImageSize);
                    else
                        selected = CvRegion.GenEmptyObj();
                    labels.Dispose();
                    stats.Dispose();
                    return;
                }
            }

            if (numLabels <= 1)
            {
                double totalArea = Cv2.CountNonZero(mask);
                if (totalArea >= minArea && totalArea <= maxArea)
                    selected = new CvRegion(mask.Clone(), region.ImageSize);
                else
                    selected = CvRegion.GenEmptyObj();
                if (ownsTempMats) { labels.Dispose(); stats.Dispose(); }
                return;
            }

            bool needShapeFilter =
                minCircularity > 0 || maxCircularity < 1 ||
                minRectangularity > 0 || maxRectangularity < 1;

            var filteredIndices = new List<int>();
            for (int i = 1; i < numLabels; i++)
            {
                int blobArea = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                if (blobArea == 0) continue;
                if (blobArea < minArea || blobArea > maxArea) continue;

                if (needShapeFilter)
                {
                    double rect = ComputeRectangularityFromStats(stats, i);
                    if (rect < minRectangularity || rect > maxRectangularity) continue;

                    if (minCircularity > 0 || maxCircularity < 1)
                    {
                        using var blobMask = new Mat();
                        Cv2.Compare(labels, i, blobMask, CmpType.EQ);
                        var bbox = Cv2.BoundingRect(blobMask);
                        var (_, circ, _) = ComputeShapeFeaturesFromMask(blobMask, blobArea, bbox);
                        if (circ < minCircularity || circ > maxCircularity) continue;
                    }
                }

                filteredIndices.Add(i);
            }

            if (filteredIndices.Count == 0)
            {
                selected = CvRegion.GenEmptyObj();
                if (ownsTempMats) { labels.Dispose(); stats.Dispose(); }
                return;
            }

            int M = filteredIndices.Count;
            var resultLabels = BuildFilteredLabels(labels, filteredIndices, region.ImageSize);
            var resultStats = FilterCCRows(stats, filteredIndices, 5, MatType.CV_32SC1);

            Mat resultCentroids;
            if (region.Centroids != null && !region.Centroids.Empty())
                resultCentroids = FilterCCRows(region.Centroids!, filteredIndices, 2, MatType.CV_64FC1);
            else
                resultCentroids = new Mat();

            var resultItems = new List<RegionItem>(M);
            for (int i = 0; i < M; i++)
                resultItems.Add(new RegionItem(resultLabels, i + 1, region.ImageSize));

            selected = new CvRegion(resultItems, region.ImageSize, resultLabels, resultStats, resultCentroids);

            if (ownsTempMats) { labels.Dispose(); stats.Dispose(); }
        }

        /// <summary>
        /// 将多区域拆分为单区域数组 - 对应 Halcon connection + 遍历子区域
        /// </summary>
        public static void Split(CvRegion region, out CvRegion[] regions)
        {
            if (region.IsEmpty)
            {
                regions = Array.Empty<CvRegion>();
                return;
            }

            int n = region.Count;
            regions = new CvRegion[n];
            for (int i = 0; i < n; i++)
            {
                var itemMask = region._items[i].GetMask();
                if (itemMask == null || itemMask.Empty())
                {
                    regions[i] = CvRegion.GenEmptyObj();
                    continue;
                }
                regions[i] = new CvRegion(itemMask.Clone(), region.ImageSize);
            }
        }

        /// <summary>
        /// 选取指定索引的子区域 - 对应 Halcon select_obj(Regions, ObjectTrans, Index1, Index2)
        /// </summary>
        public static void SelectObj(CvRegion region, int index, out CvRegion singleRegion)
        {
            if (index < 0 || index >= region.Count)
            {
                singleRegion = CvRegion.GenEmptyObj();
                return;
            }

            var itemMask = region._items[index].GetMask();
            if (itemMask == null || itemMask.Empty())
            {
                singleRegion = CvRegion.GenEmptyObj();
                return;
            }
            singleRegion = new CvRegion(itemMask.Clone(), region.ImageSize);
        }

        /// <summary>
        /// 区域特征查询 - 对应 Halcon region_features(SingleRegion : : Features : Values)
        /// </summary>
        public static void RegionFeatures(CvRegion region, out double[] values, params RegionFeature[] features)
        {
            if (features == null || features.Length == 0)
            {
                values = Array.Empty<double>();
                return;
            }

            var blobMask = region.Mask;

            double area = Cv2.CountNonZero(blobMask);
            Rect bbox = Cv2.BoundingRect(blobMask);
            var moments = Cv2.Moments(blobMask);
            double cx = moments.M00 > 0 ? moments.M10 / moments.M00 : 0;
            double cy = moments.M00 > 0 ? moments.M01 / moments.M00 : 0;

            Lazy<double?> lazyPerimeter = new(() => ComputePerimeter(blobMask));
            Lazy<double?> lazyContLength = new(() => ComputeContLength(blobMask));
            Lazy<RotatedRect> lazyMinRect2 = new(() => Cv2.MinAreaRect(blobMask));
            Lazy<Point[]>? lazyConvexHull = null;
            Lazy<(Point2f center, float radius)>? lazyMinCircle = null;
            Lazy<RotatedRect>? lazyEllipse = null;

            values = new double[features.Length];
            for (int i = 0; i < features.Length; i++)
            {
                values[i] = features[i] switch
                {
                    RegionFeature.Area => area,
                    RegionFeature.Row => cy,
                    RegionFeature.Column => cx,

                    RegionFeature.Perimeter => lazyPerimeter.Value ?? 0,
                    RegionFeature.ContLength => lazyContLength.Value ?? 0,

                    RegionFeature.Circularity => ComputeFormFactor(area, lazyPerimeter),
                    RegionFeature.Compactness => ComputeFormFactor(area, lazyPerimeter),

                    RegionFeature.Rectangularity => ComputeRectangularity(area, bbox),

                    RegionFeature.Convexity => ComputeConvexity(area, blobMask, ref lazyConvexHull),

                    RegionFeature.BoundingBoxRow => bbox.Y,
                    RegionFeature.BoundingBoxColumn => bbox.X,
                    RegionFeature.BoundingBoxWidth => bbox.Width,
                    RegionFeature.BoundingBoxHeight => bbox.Height,

                    RegionFeature.MinRect2CenterRow => lazyMinRect2.Value.Center.Y,
                    RegionFeature.MinRect2CenterColumn => lazyMinRect2.Value.Center.X,
                    RegionFeature.MinRect2Phi => lazyMinRect2.Value.Angle * Math.PI / 180.0,
                    RegionFeature.MinRect2Length1 => Math.Max(lazyMinRect2.Value.Size.Width, lazyMinRect2.Value.Size.Height),
                    RegionFeature.MinRect2Length2 => Math.Min(lazyMinRect2.Value.Size.Width, lazyMinRect2.Value.Size.Height),

                    RegionFeature.EllipticAxisRa => ComputeEllipticAxisRa(blobMask, ref lazyEllipse),
                    RegionFeature.EllipticAxisRb => ComputeEllipticAxisRb(blobMask, ref lazyEllipse),
                    RegionFeature.EllipticAxisPhi =>
                        (lazyEllipse ??= new(() => Cv2.FitEllipse(blobMask))).Value.Angle * Math.PI / 180.0,

                    RegionFeature.CircleRadius => ComputeCircleRadius(blobMask, ref lazyMinCircle),
                    RegionFeature.CircleRow =>
                        (lazyMinCircle ??= new(() => { Cv2.MinEnclosingCircle(blobMask, out var c, out var r); return (c, r); })).Value.center.Y,
                    RegionFeature.CircleColumn =>
                        (lazyMinCircle ??= new(() => { Cv2.MinEnclosingCircle(blobMask, out var c, out var r); return (c, r); })).Value.center.X,

                    _ => throw new ArgumentException($"不支持的特征类型: {features[i]}")
                };
            }
        }

        /// <summary>
        /// 多区域特征查询 - 对应 Halcon region_features(Regions, Features, Values)
        /// </summary>
        public static void RegionFeatures(CvRegion region, RegionFeature[] features, out double[][] values)
        {
            if (features == null || features.Length == 0 || region.IsEmpty)
            {
                values = Array.Empty<double[]>();
                return;
            }

            int n = region.Count;
            values = new double[features.Length][];

            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var stats = region.Stats;
                var centroids = region.Centroids;

                for (int fi = 0; fi < features.Length; fi++)
                {
                    values[fi] = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        int labelIdx = i + 1;
                        values[fi][i] = features[fi] switch
                        {
                            RegionFeature.Area => stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Area),
                            RegionFeature.Row => centroids != null && !centroids.Empty()
                                ? centroids.At<double>(labelIdx, 1) : 0,
                            RegionFeature.Column => centroids != null && !centroids.Empty()
                                ? centroids.At<double>(labelIdx, 0) : 0,
                            RegionFeature.BoundingBoxRow => stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Top),
                            RegionFeature.BoundingBoxColumn => stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Left),
                            RegionFeature.BoundingBoxWidth => stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Width),
                            RegionFeature.BoundingBoxHeight => stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Height),
                            RegionFeature.Rectangularity => ComputeRectangularityFromStats(stats, labelIdx),
                            _ => 0
                        };
                    }
                }
                return;
            }

            Split(region, out var splitRegions);
            try
            {
                for (int fi = 0; fi < features.Length; fi++)
                    values[fi] = new double[n];

                for (int i = 0; i < splitRegions.Length && i < n; i++)
                {
                    RegionFeatures(splitRegions[i], out var singleValues, features);
                    for (int fi = 0; fi < features.Length; fi++)
                        values[fi][i] = singleValues[fi];
                }
            }
            finally
            {
                foreach (var r in splitRegions) r.Dispose();
            }
        }

        /// <summary>
        /// 最小外接矩形1 - 对应 Halcon smallest_rectangle1(Regions, Row1, Col1, Row2, Col2)
        /// </summary>
        public static void SmallestRectangle1(
            CvRegion region,
            out double[] row1, out double[] col1, out double[] row2, out double[] col2)
        {
            if (region.IsEmpty || region.Count == 0)
            {
                row1 = col1 = row2 = col2 = Array.Empty<double>();
                return;
            }

            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var stats = region.Stats;
                int n = region.Count;
                row1 = new double[n];
                col1 = new double[n];
                row2 = new double[n];
                col2 = new double[n];

                for (int i = 0; i < n; i++)
                {
                    int labelIdx = i + 1;
                    int left = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Left);
                    int top = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Top);
                    int width = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Width);
                    int height = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Height);

                    row1[i] = top;
                    col1[i] = left;
                    row2[i] = top + height - 1;
                    col2[i] = left + width - 1;
                }
                return;
            }

            Split(region, out var splitRegions);
            try
            {
                int n = splitRegions.Length;
                row1 = new double[n];
                col1 = new double[n];
                row2 = new double[n];
                col2 = new double[n];

                for (int i = 0; i < n; i++)
                {
                    var bbox = Cv2.BoundingRect(splitRegions[i].Mask);
                    row1[i] = bbox.Y;
                    col1[i] = bbox.X;
                    row2[i] = bbox.Y + bbox.Height - 1;
                    col2[i] = bbox.X + bbox.Width - 1;
                }
            }
            finally
            {
                foreach (var r in splitRegions) r.Dispose();
            }
        }

        /// <summary>
        /// 矩形度 - 对应 Halcon rectangularity(Regions, Rectangularity)
        /// </summary>
        public static void Rectangularity(CvRegion region, out double[] rectangularity)
        {
            if (region.IsEmpty || region.Count == 0)
            {
                rectangularity = Array.Empty<double>();
                return;
            }

            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var stats = region.Stats;
                int n = region.Count;
                rectangularity = new double[n];

                for (int i = 0; i < n; i++)
                    rectangularity[i] = ComputeRectangularityFromStats(stats, i + 1);

                return;
            }

            Split(region, out var splitRegions);
            try
            {
                int n = splitRegions.Length;
                rectangularity = new double[n];
                for (int i = 0; i < n; i++)
                {
                    var mask = splitRegions[i].Mask;
                    double area = Cv2.CountNonZero(mask);
                    var bbox = Cv2.BoundingRect(mask);
                    rectangularity[i] = ComputeRectangularity(area, bbox);
                }
            }
            finally
            {
                foreach (var r in splitRegions) r.Dispose();
            }
        }

        /// <summary>
        /// 获取轮廓 - 对应 Halcon get_contour_xld(Contour, Y, X)
        /// </summary>
        public static void GetContour(CvRegion region, out CvContour[] contours)
        {
            if (region.IsEmpty)
            {
                contours = Array.Empty<CvContour>();
                return;
            }

            int n = region.Count;
            contours = new CvContour[n];

            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var labels = region.Labels!;
                var stats = region.Stats!;

                for (int i = 0; i < n; i++)
                {
                    int labelIdx = i + 1;
                    int left = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Left);
                    int top = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Top);
                    int width = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Width);
                    int height = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Height);

                    if (width <= 0 || height <= 0)
                    {
                        contours[i] = CvContour.GenEmptyObj();
                        continue;
                    }

                    var bbox = new Rect(left, top, width, height);
                    using var labelRoi = new Mat(labels, bbox);
                    using var maskRoi = new Mat();
                    Cv2.Compare(labelRoi, labelIdx, maskRoi, CmpType.EQ);

                    Cv2.FindContours(maskRoi, out var blobContours, out _,
                        RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                    if (blobContours.Length > 0)
                    {
                        var points = blobContours[0]
                            .Select(p => new Point(p.X + left, p.Y + top))
                            .ToArray();
                        contours[i] = CvContour.Create(points, true, region.ImageSize);
                    }
                    else
                        contours[i] = CvContour.GenEmptyObj();
                }
                return;
            }

            for (int i = 0; i < n; i++)
            {
                var itemMask = region._items[i].GetMask();
                if (itemMask == null || itemMask.Empty())
                {
                    contours[i] = CvContour.GenEmptyObj();
                    continue;
                }

                var bbox = Cv2.BoundingRect(itemMask);
                if (bbox.Width <= 0 || bbox.Height <= 0)
                {
                    contours[i] = CvContour.GenEmptyObj();
                    continue;
                }

                using var cropped = new Mat(itemMask, bbox);
                Cv2.FindContours(cropped, out var blobContours, out _,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (blobContours.Length > 0)
                {
                    var points = blobContours[0]
                        .Select(p => new Point(p.X + bbox.X, p.Y + bbox.Y))
                        .ToArray();
                    contours[i] = CvContour.Create(points, true, region.ImageSize);
                }
                else
                    contours[i] = CvContour.GenEmptyObj();
            }
        }

        /// <summary>
        /// 轮廓圆度 - 对应 Halcon circularity(XLDContours, Circularity)
        /// </summary>
        public static void Circularity(CvContour[] contours, out double[] circularity)
        {
            circularity = new double[contours.Length];
            for (int i = 0; i < contours.Length; i++)
            {
                var c = contours[i];
                if (c.IsEmpty || c.Points == null || c.Points.Length < 3)
                {
                    circularity[i] = 0;
                    continue;
                }

                double perimeter = Cv2.ArcLength(c.Points, c.IsClosed);
                double area = Math.Abs(Cv2.ContourArea(c.Points));

                if (perimeter <= 0 || area <= 0)
                {
                    circularity[i] = 0;
                    continue;
                }

                circularity[i] = Math.Min(1.0, (4.0 * Math.PI * area) / (perimeter * perimeter));
            }
        }

        /// <summary>
        /// 轮廓长度 - 对应 Halcon cont_length(Contours, Length)
        /// </summary>
        public static void ContLength(CvContour[] contours, out double[] length)
        {
            length = new double[contours.Length];
            for (int i = 0; i < contours.Length; i++)
            {
                var c = contours[i];
                if (c.IsEmpty || c.Points == null || c.Points.Length < 2)
                {
                    length[i] = 0;
                    continue;
                }
                length[i] = Cv2.ArcLength(c.Points, c.IsClosed);
            }
        }

        #endregion

        #region 私有辅助 — 形状特征计算（被 RegionFeatures/SelectShape 内部调用）

        private static double ComputeRectangularityFromStats(Mat stats, int labelIdx)
        {
            int area = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Area);
            int width = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Width);
            int height = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Height);
            double bboxArea = width * height;
            return bboxArea > 0 ? area / bboxArea : 0;
        }

        private static (double perimeter, double circularity, double rectangularity) ComputeShapeFeaturesFromMask(
            Mat blobMask, double area, Rect bbox)
        {
            double perimeter = 0, circularity = 0, rectangularity = 0;

            Cv2.FindContours(blobMask, out var contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length > 0)
            {
                perimeter = Cv2.ArcLength(contours[0], true);
                circularity = perimeter > 0
                    ? (4 * Math.PI * area) / (perimeter * perimeter)
                    : 0;
            }

            double bboxArea = bbox.Width * bbox.Height;
            rectangularity = bboxArea > 0 ? area / bboxArea : 0;

            return (perimeter, circularity, rectangularity);
        }

        private static double ComputePerimeter(Mat blobMask)
        {
            Cv2.FindContours(blobMask, out var contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            return contours.Length > 0 ? Cv2.ArcLength(contours[0], true) : 0;
        }

        private static double ComputeContLength(Mat blobMask)
        {
            Cv2.FindContours(blobMask, out var contours, out _,
                RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            double total = 0;
            for (int i = 0; i < contours.Length; i++)
                total += Cv2.ArcLength(contours[i], true);
            return total;
        }

        private static double ComputeFormFactor(double area, Lazy<double?> lazyPerimeter)
        {
            double p = lazyPerimeter.Value ?? 0;
            if (p <= 0 || area <= 0) return double.MaxValue;
            return (p * p) / (4.0 * Math.PI * area);
        }

        private static double ComputeRectangularity(double area, Rect axisBbox)
        {
            double rectArea = axisBbox.Width * axisBbox.Height;
            return rectArea > 0 ? area / rectArea : 0;
        }

        private static double ComputeConvexity(double area, Mat blobMask, ref Lazy<Point[]?> lazyConvexHull)
        {
            lazyConvexHull ??= new(() => GetConvexHull(blobMask));
            var hull = lazyConvexHull.Value;
            if (hull == null || hull.Length < 3) return 1.0;
            return area / Cv2.ContourArea(hull);
        }

        private static double ComputeEllipticAxisRa(Mat blobMask, ref Lazy<RotatedRect>? lazyEllipse)
        {
            lazyEllipse ??= new(() => Cv2.FitEllipse(blobMask));
            var ellipse = lazyEllipse.Value;
            return Math.Max(ellipse.Size.Width, ellipse.Size.Height) / 2.0;
        }

        private static double ComputeEllipticAxisRb(Mat blobMask, ref Lazy<RotatedRect>? lazyEllipse)
        {
            lazyEllipse ??= new(() => Cv2.FitEllipse(blobMask));
            var ellipse = lazyEllipse.Value;
            return Math.Min(ellipse.Size.Width, ellipse.Size.Height) / 2.0;
        }

        private static double ComputeCircleRadius(Mat blobMask, ref Lazy<(Point2f center, float radius)>? lazyMinCircle)
        {
            lazyMinCircle ??= new(() =>
            {
                Cv2.MinEnclosingCircle(blobMask, out var c, out var r);
                return (c, r);
            });
            return lazyMinCircle.Value.radius;
        }

        private static Point[]? GetConvexHull(Mat blobMask)
        {
            Cv2.FindContours(blobMask, out var contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0) return null;
            return Cv2.ConvexHull(contours[0]);
        }

        #endregion
    }
}
