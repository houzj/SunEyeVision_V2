using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 区域特征类型 - 对应 Halcon region_features 的特征名列表
    /// </summary>
    /// <remarks>
    /// 使用示例（对齐 Halcon）:
    ///   region_features(SingleRegion, ['area', 'perimeter', 'circularity'], [A, P, C])
    ///   ↓
    ///   CvOp.RegionFeatures(singleRegion,
    ///       RegionFeature.Area, RegionFeature.Perimeter, RegionFeature.Circularity,
    ///       out var values)
    /// 
    /// 公式说明：
    ///   Circularity = Compactness = P²/(4πA) （Halcon 定义，值越大越不圆）
    ///   Rectangularity = A / minEnclosingRectArea（最小外接矩形面积比）
    ///   Convexity = A / convexHullArea（凸包面积比）
    /// </remarks>
    public enum RegionFeature
    {
        // ===== 基础几何（对应 Halcon area_center）=====

        Area,           // 面积（像素数）
        Row,            // 质心行坐标 (cy)
        Column,         // 质心列坐标 (cx)

        // ===== 周长与轮廓 =====

        Perimeter,          // 区域周长（外轮廓 ArcLength，closed=true）
        ContLength,         // 轮廓长度（所有内外轮廓之和，RetrievalModes.List）

        // ===== 形状特征 =====

        Circularity,        // 圆度 F = P²/(4πA) — Halcon 定义（越大越偏离圆形）
        Compactness,        // 紧凑度 F = P²/(4πA) — 与圆度公式相同
        Rectangularity,     // 矩形度 = A / minEnclosingRectArea（最小外接矩形）

        // ===== 凸性 =====

        Convexity,          // 凸性 = A / convexHullArea（区域面积/凸包面积）

        // ===== 外接矩形1 - axis-aligned bounding box（smallest_rectangle1）=====

        BoundingBoxRow,         // 左上角行坐标 Row1
        BoundingBoxColumn,      // 左上角列坐标 Column1
        BoundingBoxWidth,       // 宽度
        BoundingBoxHeight,      // 高度

        // ===== 外接矩形2 - 最小面积旋转矩形（smallest_rectangle2）=====

        MinRect2CenterRow,      // 中心点行坐标
        MinRect2CenterColumn,   // 中心点列坐标
        MinRect2Phi,            // 方向角度（弧度）
        MinRect2Length1,        // 第一边长度（长边）
        MinRect2Length2,        // 第二边长度（短边）

        // ===== 椭圆轴（elliptic_axis）=====

        EllipticAxisRa,     // 长轴半径 Ra
        EllipticAxisRb,     // 短轴半径 Rb
        EllipticAxisPhi,    // 主轴方向 Phi（弧度）

        // ===== 外接圆（smallest_circle / min_enclosing_circle）=====

        CircleRadius,       // 最小外接圆半径
        CircleRow,          // 外接圆圆心行
        CircleColumn,       // 外接圆圆心列
    }

    /// <summary>
    /// 视觉算子集合 - 所有图像/区域操作的唯一入口
    /// </summary>
    /// <remarks>
    /// 设计原则：
    ///   - 类型定义（CvImage/CvRegion）与算法实现完全解耦
    ///   - CvRegion 是纯数据载体，不含任何算法逻辑
    ///   - 所有操作通过本类静态方法执行
    /// 
    /// 签名约定（严格对齐 Halcon）：
    ///   所有算子统一使用 void 返回 + out 参数输出结果。
    ///   Halcon: connection(Region : : : ConnectedRegions)
    ///   C#:    Connection(region, out var connectedRegions)
    /// 
    /// 算子分组：
    ///   📦 图像转换  Rgb1ToGray, ReduceDomain
    ///   ✂️ 阈值分割  Threshold, AdaptiveThreshold
    ///   📊 统计     Intensity
    ///   🖼️ 显示     ComposeDisplay
    ///   🔬 区域算子  Connection, AreaCenter, SelectShape, Split, SelectObj
    ///   🔧 集合运算  Union1, Difference, Intersection, Complement, Concat
    ///   ⭕ 形态学    DilationCircle, ErosionCircle, DilationRectangle1, ErosionRectangle1
    ///               OpeningCircle, ClosingCircle
    ///   📋 其他    CopyObj
    /// </remarks>
    public static class CvOp
    {
        #region 📦 图像转换

        /// <summary>
        /// RGB转灰度 - 对应 Halcon rgb1_to_gray(Image, GrayImage)
        /// </summary>
        public static void Rgb1ToGray(Mat input, out CvImage grayImage)
        {
            if (input.Channels() == 1 && input.Depth() == MatType.CV_8U)
            {
                grayImage = new CvImage(input.Clone(), ownsImage: true);
                return;
            }

            var gray = new Mat();
            if (input.Channels() > 1)
            {
                Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                input.ConvertTo(gray, MatType.CV_8UC1);
            }
            grayImage = new CvImage(gray, ownsImage: true);
        }

        /// <summary>
        /// 缩小定义域 - 对应 Halcon reduce_domain(Image, Region, ImageReduced)
        /// </summary>
        public static void ReduceDomain(CvImage image, CvRegion? region, out CvImage imageReduced)
        {
            if (region == null || region.IsEmpty)
            {
                imageReduced = new CvImage(image.Image.Clone(), ownsImage: true);
                return;
            }
            using var masked = new Mat();
            image.Image.CopyTo(masked, region.Mask);
            imageReduced = new CvImage(masked, ownsImage: true);
        }

        #endregion

        #region ✂️ 阈值分割

        /// <summary>
        /// 阈值分割 - 对应 Halcon threshold(Region, Image, out Region, Min, Max)
        /// </summary>
        public static void Threshold(
            CvRegion? region, CvImage image,
            int thresh, int maxVal,
            out CvRegion regionResult, out double actualThreshold,
            CvThresholdType type = CvThresholdType.Binary,
            bool invert = false)
        {
            using var fullBinary = new Mat();
            actualThreshold = Cv2.Threshold(image.Image, fullBinary, thresh, maxVal,
                CvConvert.ToOpenCvThresholdType(type));

            ApplyMaskAndInvert(fullBinary, region?.Mask, invert, image.Size, out regionResult);
        }

        /// <summary>
        /// 自适应阈值分割 - 对应 Halcon adaptive_threshold(Region, Image, ...)
        /// </summary>
        public static void AdaptiveThreshold(
            CvRegion? region, CvImage image,
            int maxValue, CvAdaptiveMethod method, int blockSize,
            out CvRegion regionResult,
            double c = 5,
            CvThresholdType type = CvThresholdType.Binary,
            bool invert = false)
        {
            using var fullBinary = new Mat();
            Cv2.AdaptiveThreshold(
                image.Image, fullBinary, maxValue,
                CvConvert.ToOpenCvAdaptiveType(method),
                CvConvert.ToOpenCvThresholdType(type),
                blockSize, c);

            ApplyMaskAndInvert(fullBinary, region?.Mask, invert, image.Size, out regionResult);
        }

        private static void ApplyMaskAndInvert(Mat fullBinary, Mat? mask, bool invert, Size imageSize, out CvRegion result)
        {
            if (mask != null && !mask.Empty())
            {
                var masked = new Mat();
                Cv2.BitwiseAnd(fullBinary, fullBinary, masked, mask);
                result = CvRegion.FromMask(masked, ownsData: true);
            }
            else
            {
                result = CvRegion.FromMask(fullBinary.Clone(), ownsData: true);
            }

            if (invert)
            {
                Complement(result, out var complemented);
                result.Dispose();
                result = complemented;
            }
        }

        #endregion

        #region 📊 统计

        /// <summary>
        /// 强度统计 - 对应 Halcon intensity(Regions, Image, Mean, Deviation)
        /// </summary>
        public static void Intensity(
            CvRegion? region, CvImage image,
            out double[] histogram,
            out double mean, out double stdDev)
        {
            var mat = image.Image;

            using var histMat = new Mat();
            Cv2.CalcHist(new[] { mat }, new[] { 0 }, region?.Mask, histMat,
                1, new[] { 256 }, new Rangef[] { new(0f, 256f) }, true);

            var histData = new double[256];
            for (int i = 0; i < 256; i++)
                histData[i] = histMat.At<float>(i);
            histogram = histData;

            using var meanMat = new Mat();
            using var stdDevMat = new Mat();
            Cv2.MeanStdDev(mat, meanMat, stdDevMat, region?.Mask);

            mean = meanMat.Get<double>(0);
            stdDev = stdDevMat.Get<double>(0);
        }

        #endregion

        #region 🖼️ 显示

        /// <summary>
        /// 组合显示图像 - 对应 Halcon dev_display 组合效果
        /// </summary>
        public static void ComposeDisplay(
            CvRegion? region, CvImage original, CvRegion binaryRegion,
            out Mat displayImage)
        {
            if (region == null || region.IsEmpty)
            {
                displayImage = binaryRegion.Mask?.Clone() ??
                    new Mat(original.Size, MatType.CV_8UC1, Scalar.Black);
                return;
            }

            displayImage = original.Image.Clone();
            var binaryMask = binaryRegion.Mask;
            if (binaryMask != null && !binaryMask.Empty())
                binaryMask.CopyTo(displayImage, region.Mask);
        }

        #endregion

        #region 🔬 连通域与特征查询

        /// <summary>
        /// 连通域分析 - 对应 Halcon connection(Region : : : ConnectedRegions)
        /// 产出完整 CC 数据（labels + stats + centroids），下游算子可直接复用零开销
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
                connectedRegions = new CvRegion(
                    new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black),
                    0, region.ImageSize);
                return;
            }

            // 从 labels 派生 mask，保证 mask 与 labels 一致性
            var derivedMask = new Mat();
            Cv2.Compare(labels, 0, derivedMask, CmpType.GT);

            // 转移所有权给 CvRegion（不用 using）
            connectedRegions = new CvRegion(
                derivedMask, labels, stats, centroids,
                numLabels - 1, region.ImageSize);
        }

        /// <summary>
        /// 面积和质心查询 - 对应 Halcon area_center(Regions : : : Area, Row, Column)
        /// 优先复用已缓存的 stats/centroids（零 CC 开销）
        /// </summary>
        public static void AreaCenter(CvRegion region, out (double Area, double Row, double Column)[] areaCenters)
        {
            if (region.IsEmpty)
            {
                areaCenters = Array.Empty<(double, double, double)>();
                return;
            }

            // 快速路径：复用已缓存的 stats + centroids（零 CC 开销）
            if (region.Stats != null && !region.Stats.Empty()
                && region.Centroids != null && !region.Centroids.Empty())
            {
                var stats = region.Stats;
                var centroids = region.Centroids;
                int n = stats.Rows; // 含背景行: [0]=背景, [1..n-1]=子区域
                areaCenters = new (double, double, double)[n - 1];
                for (int i = 1; i < n; i++)
                    areaCenters[i - 1] = (
                        stats.At<int>(i, (int)ConnectedComponentsTypes.Area),
                        centroids.At<double>(i, 1),   // cy (Row)
                        centroids.At<double>(i, 0));  // cx (Column)
                return;
            }

            // 慢速路径：无缓存时重新跑 ConnectedComponentsWithStats
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
        /// 复用已有 labels/stats（零 CC 开销），输出保留 labels（过滤后重编号）
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

            // 快速路径：复用已有 labels+stats（零 CC 开销）
            Mat labels;
            Mat stats;
            int numLabels;

            if (hasCached)
            {
                labels = region.Labels!;   // 只读引用，不拷贝
                stats = region.Stats!;
                numLabels = stats.Rows;
            }
            else
            {
                // 慢速路径：重新计算 CC
                var tmpLabels = new Mat();
                var tmpStats = new Mat();
                using var tmpCentroids = new Mat();
                numLabels = Cv2.ConnectedComponentsWithStats(
                    mask, tmpLabels, tmpStats, tmpCentroids, PixelConnectivity.Connectivity8);
                labels = tmpLabels;
                stats = tmpStats;

                if (numLabels <= 1)
                {
                    double totalArea = Cv2.CountNonZero(mask);
                    if (totalArea >= minArea && totalArea <= maxArea)
                        selected = new CvRegion(mask.Clone(), 1, region.ImageSize);
                    else
                        selected = CvRegion.GenEmptyObj();
                    if (!hasCached) { labels.Dispose(); stats.Dispose(); }
                    return;
                }
            }

            if (numLabels <= 1)
            {
                double totalArea = Cv2.CountNonZero(mask);
                if (totalArea >= minArea && totalArea <= maxArea)
                    selected = new CvRegion(mask.Clone(), 1, region.ImageSize);
                else
                    selected = CvRegion.GenEmptyObj();
                return;
            }

            bool needShapeFilter =
                minCircularity > 0 || maxCircularity < 1 ||
                minRectangularity > 0 || maxRectangularity < 1;

            var filteredIndices = new List<int>();
            for (int i = 1; i < numLabels; i++)
            {
                using var blobMask = new Mat();
                Cv2.Compare(labels, i, blobMask, CmpType.EQ);

                double blobArea = Cv2.CountNonZero(blobMask);
                if (blobArea == 0) continue;
                if (blobArea < minArea || blobArea > maxArea) continue;

                if (needShapeFilter)
                {
                    var bbox = Cv2.BoundingRect(blobMask);
                    var (_, circ, rect) = ComputeShapeFeaturesFromMask(blobMask, blobArea, bbox);
                    if (circ < minCircularity || circ > maxCircularity) continue;
                    if (rect < minRectangularity || rect > maxRectangularity) continue;
                }

                filteredIndices.Add(i);
            }

            if (filteredIndices.Count == 0)
            {
                selected = new CvRegion(
                    new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black),
                    0, region.ImageSize);
                return;
            }

            // 构建合并掩码
            int M = filteredIndices.Count;
            var resultMask = new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black);
            foreach (var idx in filteredIndices)
            {
                using var blobMask = new Mat();
                Cv2.Compare(labels, idx, blobMask, CmpType.EQ);
                Cv2.BitwiseOr(resultMask, 255, resultMask, blobMask);
            }

            // 重建 labels（过滤 + 重编号 1..M），使下游继续受益于缓存
            var resultLabels = BuildFilteredLabels(labels, filteredIndices, region.ImageSize);
            var resultStats = FilterCCRows(stats, filteredIndices, 5, MatType.CV_32SC1);

            // centroids 可能来自缓存或临时变量，统一处理
            Mat resultCentroids;
            if (region.Centroids != null && !region.Centroids.Empty())
                resultCentroids = FilterCCRows(region.Centroids!, filteredIndices, 2, MatType.CV_64FC1);
            else
                resultCentroids = new Mat(); // 空占位

            selected = new CvRegion(resultMask, resultLabels, resultStats, resultCentroids,
                M, region.ImageSize);
        }

        /// <summary>
        /// 将多区域拆分为单区域数组 - 对应 Halcon connection + 遍历子区域
        /// 优先复用 labels（零 CC 开销）
        /// </summary>
        public static void Split(CvRegion region, out CvRegion[] regions)
        {
            if (region.IsEmpty)
            {
                regions = Array.Empty<CvRegion>();
                return;
            }

            // 快速路径：复用 labels（零 CC 开销）
            if (region.HasLabels)
            {
                int n = region.Count;
                var cachedLabels = region.Labels!;
                regions = new CvRegion[n];
                for (int i = 0; i < n; i++)
                {
                    using var blobMask = new Mat();
                    // CC 标签值 = i + 1（0 是背景）
                    Cv2.Compare(cachedLabels, i + 1, blobMask, CmpType.EQ);
                    regions[i] = CvRegion.FromMask(blobMask);
                }
                return;
            }

            // 慢速路径：无缓存时重新跑 ConnectedComponentsWithStats
            var mask = region.Mask;
            if (mask == null || mask.Empty())
            {
                regions = Array.Empty<CvRegion>();
                return;
            }

            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            int numLabels = Cv2.ConnectedComponentsWithStats(
                mask, labels, stats, centroids, PixelConnectivity.Connectivity8);

            if (numLabels <= 1)
            {
                regions = Array.Empty<CvRegion>();
                return;
            }

            regions = new CvRegion[numLabels - 1];
            for (int i = 1; i < numLabels; i++)
            {
                using var blobMask = new Mat();
                Cv2.Compare(labels, i, blobMask, CmpType.EQ);
                regions[i - 1] = CvRegion.FromMask(blobMask);
            }
        }

        /// <summary>
        /// 选取指定索引的子区域 - 对应 Halcon select_obj(Regions, ObjectTrans, Index1, Index2)
        /// </summary>
        /// <param name="region">多区域对象（需有 labels 缓存）</param>
        /// <param name="index">子区域索引（0-based，范围 0..Count-1）</param>
        /// <param name="singleRegion">选中的单个子区域</param>
        public static void SelectObj(CvRegion region, int index, out CvRegion singleRegion)
        {
            if (index < 0 || index >= region.Count)
            {
                singleRegion = CvRegion.GenEmptyObj();
                return;
            }
            if (!region.HasLabels)
            {
                singleRegion = CvRegion.GenEmptyObj();
                return;
            }

            // CC 内部标签值 = index + 1
            using var blobMask = new Mat();
            Cv2.Compare(region.Labels!, index + 1, blobMask, CmpType.EQ);
            singleRegion = CvRegion.FromMask(blobMask);
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

                    RegionFeature.Circularity =>
                        ComputeFormFactor(area, lazyPerimeter),
                    RegionFeature.Compactness =>
                        ComputeFormFactor(area, lazyPerimeter),

                    RegionFeature.Rectangularity =>
                        ComputeRectangularity(area, bbox),

                    RegionFeature.Convexity =>
                        ComputeConvexity(area, blobMask, ref lazyConvexHull),

                    RegionFeature.BoundingBoxRow => bbox.Y,
                    RegionFeature.BoundingBoxColumn => bbox.X,
                    RegionFeature.BoundingBoxWidth => bbox.Width,
                    RegionFeature.BoundingBoxHeight => bbox.Height,

                    RegionFeature.MinRect2CenterRow => lazyMinRect2.Value.Center.Y,
                    RegionFeature.MinRect2CenterColumn => lazyMinRect2.Value.Center.X,
                    RegionFeature.MinRect2Phi => lazyMinRect2.Value.Angle * Math.PI / 180.0,
                    RegionFeature.MinRect2Length1 => Math.Max(
                        lazyMinRect2.Value.Size.Width,
                        lazyMinRect2.Value.Size.Height),
                    RegionFeature.MinRect2Length2 => Math.Min(
                        lazyMinRect2.Value.Size.Width,
                        lazyMinRect2.Value.Size.Height),

                    RegionFeature.EllipticAxisRa =>
                        ComputeEllipticAxisRa(blobMask, ref lazyEllipse),
                    RegionFeature.EllipticAxisRb =>
                        ComputeEllipticAxisRb(blobMask, ref lazyEllipse),
                    RegionFeature.EllipticAxisPhi =>
                        (lazyEllipse ??= new(() => Cv2.FitEllipse(blobMask))).Value.Angle * Math.PI / 180.0,

                    RegionFeature.CircleRadius =>
                        ComputeCircleRadius(blobMask, ref lazyMinCircle),
                    RegionFeature.CircleRow =>
                        (lazyMinCircle ??= new(() => { Cv2.MinEnclosingCircle(blobMask, out var c, out var r); return (c, r); })).Value.center.Y,
                    RegionFeature.CircleColumn =>
                        (lazyMinCircle ??= new(() => { Cv2.MinEnclosingCircle(blobMask, out var c, out var r); return (c, r); })).Value.center.X,

                    _ => throw new ArgumentException($"不支持的特征类型: {features[i]}")
                };
            }
        }

        #endregion

        #region 🔧 集合运算

        /// <summary>区域并集 - 对应 Halcon union1(Regions : : : RegionUnion)</summary>
        public static void Union1(CvRegion region, out CvRegion unionRegion)
        {
            if (region.IsEmpty)
            {
                unionRegion = CvRegion.GenEmptyObj();
                return;
            }
            unionRegion = new CvRegion(region.Mask?.Clone(), 1, region.ImageSize);
        }

        /// <summary>区域差集 - 对应 Halcon difference(Sub, Pattern : : : ResultDifference)</summary>
        public static void Difference(CvRegion sub, CvRegion pattern, out CvRegion resultDifference)
        {
            if (sub.IsEmpty)
            {
                resultDifference = CvRegion.GenEmptyObj();
                return;
            }
            if (pattern.IsEmpty)
            {
                resultDifference = new CvRegion(sub.Mask?.Clone(), sub._count, sub.ImageSize);
                return;
            }

            using var notPattern = new Mat();
            Cv2.BitwiseNot(pattern._mask!, notPattern);

            var resultMask = new Mat();
            Cv2.BitwiseAnd(sub._mask!, notPattern, resultMask);
            int count = Cv2.CountNonZero(resultMask) > 0 ? 1 : 0;
            resultDifference = new CvRegion(resultMask, count, sub.ImageSize);
        }

        /// <summary>区域交集 - 对应 Halcon intersection(Region1, Region2 : : : ResultIntersection)</summary>
        public static void Intersection(CvRegion region1, CvRegion region2, out CvRegion resultIntersection)
        {
            if (region1.IsEmpty || region2.IsEmpty)
            {
                resultIntersection = CvRegion.GenEmptyObj();
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseAnd(region1._mask!, region2._mask!, resultMask);
            int count = Cv2.CountNonZero(resultMask) > 0 ? 1 : 0;
            resultIntersection = new CvRegion(resultMask, count, region1.ImageSize);
        }

        /// <summary>区域补集 - 对应 Halcon complement(Region : : : ResultComplement)</summary>
        public static void Complement(CvRegion region, out CvRegion resultComplement)
        {
            if (region.IsEmpty)
            {
                resultComplement = CvRegion.GenEmptyObj();
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseNot(region._mask!, resultMask);
            resultComplement = new CvRegion(resultMask, 1, region.ImageSize);
        }

        /// <summary>拼接两个区域 - 对应 Halcon concat_obj(Objects1, Objects2 : : : ConcatObjects)</summary>
        public static void Concat(CvRegion region, CvRegion other, out CvRegion concatResult)
        {
            if (region.IsEmpty && other.IsEmpty)
            {
                concatResult = CvRegion.GenEmptyObj();
                return;
            }
            if (other.IsEmpty)
            {
                concatResult = new CvRegion(region._mask?.Clone(), region._count, region.ImageSize);
                return;
            }
            if (region.IsEmpty)
            {
                concatResult = new CvRegion(other._mask?.Clone(), other._count, other._imageSize);
                return;
            }

            var resultMask = new Mat();
            Cv2.BitwiseOr(region._mask!, other._mask!, resultMask);
            concatResult = new CvRegion(resultMask, region._count + other._count, region.ImageSize);
        }

        #endregion

        #region ⭕ 形态学操作

        /// <summary>圆形膨胀 - 对应 Halcon dilation_circle(Region : : : RegionDilation, Radius)</summary>
        public static void DilationCircle(CvRegion region, int radius, out CvRegion dilatedRegion)
        {
            var mask = region.Mask;
            if (mask == null)
            {
                dilatedRegion = CvRegion.GenEmptyObj();
                return;
            }

            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * radius + 1, 2 * radius + 1));
            using (kernel)
            {
                var dilated = new Mat();
                Cv2.Dilate(mask, dilated, kernel);
                dilatedRegion = new CvRegion(dilated, region._count, region.ImageSize);
            }
        }

        /// <summary>圆形腐蚀 - 对应 Halcon erosion_circle(Region : : : RegionErosion, Radius)</summary>
        public static void ErosionCircle(CvRegion region, int radius, out CvRegion erodedRegion)
        {
            var mask = region.Mask;
            if (mask == null)
            {
                erodedRegion = CvRegion.GenEmptyObj();
                return;
            }

            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * radius + 1, 2 * radius + 1));
            using (kernel)
            {
                var eroded = new Mat();
                Cv2.Erode(mask, eroded, kernel);
                erodedRegion = new CvRegion(eroded, region._count, region.ImageSize);
            }
        }

        /// <summary>矩形膨胀 - 对应 Halcon dilation_rectangle1(Region : : : RegionDilation, Width, Height)</summary>
        public static void DilationRectangle1(CvRegion region, int width, int height, out CvRegion dilatedRegion)
        {
            var mask = region.Mask;
            if (mask == null)
            {
                dilatedRegion = CvRegion.GenEmptyObj();
                return;
            }

            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(width, height));
            using (kernel)
            {
                var dilated = new Mat();
                Cv2.Dilate(mask, dilated, kernel);
                dilatedRegion = new CvRegion(dilated, region._count, region.ImageSize);
            }
        }

        /// <summary>矩形腐蚀 - 对应 Halcon erosion_rectangle1(Region : : : RegionErosion, Width, Height)</summary>
        public static void ErosionRectangle1(CvRegion region, int width, int height, out CvRegion erodedRegion)
        {
            var mask = region.Mask;
            if (mask == null)
            {
                erodedRegion = CvRegion.GenEmptyObj();
                return;
            }

            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(width, height));
            using (kernel)
            {
                var eroded = new Mat();
                Cv2.Erode(mask, eroded, kernel);
                erodedRegion = new CvRegion(eroded, region._count, region.ImageSize);
            }
        }

        /// <summary>圆形开运算 - 对应 Halcon opening_circle(Region : : : RegionOpening, Radius)</summary>
        public static void OpeningCircle(CvRegion region, int radius, out CvRegion openedRegion)
        {
            ErosionCircle(region, radius, out var eroded);
            using (eroded)
                DilationCircle(eroded, radius, out openedRegion);
        }

        /// <summary>圆形闭运算 - 对应 Halcon closing_circle(Region : : : RegionClosing, Radius)</summary>
        public static void ClosingCircle(CvRegion region, int radius, out CvRegion closedRegion)
        {
            DilationCircle(region, radius, out var dilated);
            using (dilated)
                ErosionCircle(dilated, radius, out closedRegion);
        }

        /// <summary>深拷贝 - 对应 Halcon copy_obj(Object : : : Copy)</summary>
        public static void CopyObj(CvRegion region, out CvRegion copy)
        {
            copy = region.CopyObj();
        }

        #endregion

        #region 私有辅助 — 形状特征计算

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

        #region 私有辅助 — Labels/CC 数据重建

        /// <summary>
        /// 从原始 labels 中过滤指定标签行，重编号为 1..M
        /// 用于 SelectShape 输出重建，使下游算子继续受益于缓存
        /// </summary>
        private static Mat BuildFilteredLabels(Mat sourceLabels, List<int> keepIndices, Size imageSize)
        {
            int M = keepIndices.Count;
            var result = new Mat(imageSize, MatType.CV_32SC1, Scalar.All(0));
            for (int newIndex = 0; newIndex < M; newIndex++)
            {
                int oldLabel = keepIndices[newIndex];
                using var mask = new Mat();
                Cv2.Compare(sourceLabels, oldLabel, mask, CmpType.EQ);
                result.SetTo(Scalar.All(newIndex + 1), mask); // 重编号: 1..M
            }
            return result;
        }

        /// <summary>
        /// 从 CC 统计矩阵中提取指定行（用于 SelectShape 过滤后重建 stats/centroids）
        /// </summary>
        private static Mat FilterCCRows(Mat source, List<int> keepIndices, int cols, MatType dtype)
        {
            int M = keepIndices.Count;
            var result = new Mat(M + 1, cols, dtype); // +1 行给背景占位 (row 0)
            for (int i = 0; i < M; i++)
                source.Row(keepIndices[i]).CopyTo(result.Row(i + 1));
            return result;
        }

        #endregion
    }
}
