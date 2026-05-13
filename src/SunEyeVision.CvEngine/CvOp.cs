using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Logging;

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
        ///   📦 图像转换  Rgb1ToGray, ReduceDomain, CropDomain
        ///   ✂️ 阈值分割  Threshold, AdaptiveThreshold, ThresholdRange
        ///   📊 统计     Intensity(单区域), Intensity(多区域)
    ///   🖼️ 显示     ComposeDisplay
    ///   🔬 区域算子  Connection, AreaCenter, SelectShape, Split, SelectObj
    ///                SmallestRectangle1, Rectangularity, GetContour
    ///   📐 轮廓算子  Circularity(XLD), ContLength
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
        /// RGB转灰度（图像数组）- 对应 Halcon rgb1_to_gray(Image(-array), GrayImage(-array))
        /// 逐 item 处理，输出等长 CvImage
        /// </summary>
        public static void Rgb1ToGray(CvImage input, out CvImage grayImage)
        {
            if (input.Empty)
            {
                grayImage = CvImage.GenEmptyObj();
                return;
            }

            // 快速路径：单图像
            if (input.Count == 1)
            {
                Rgb1ToGray(input.Image, out grayImage);
                return;
            }

            // 多图像路径
            var items = new List<ImageItem>(input.Count);
            foreach (var item in input._items)
            {
                var src = item.Image;
                if (src.Channels() == 1 && src.Depth() == MatType.CV_8U)
                {
                    items.Add(new ImageItem(src.Clone(), ownsImage: true));
                }
                else
                {
                    var gray = new Mat();
                    if (src.Channels() > 1)
                        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
                    else
                        src.ConvertTo(gray, MatType.CV_8UC1);
                    items.Add(new ImageItem(gray, ownsImage: true));
                }
            }
            grayImage = new CvImage(items);
        }

        /// <summary>
        /// 缩小定义域 - 对应 Halcon reduce_domain(Image, Region, ImageReduced)
        /// 不改变图像尺寸，仅用 Region 掩码限制图像的有效区域（域外像素置零）
        /// 支持 CvImage 数组：逐 item 处理，输出等长 CvImage
        /// </summary>
        public static void ReduceDomain(CvImage image, CvRegion? region, out CvImage imageReduced)
        {
            // 快速路径：无 region 或单图像
            if (region == null || region.IsEmpty)
            {
                var items = new List<ImageItem>(image.Count);
                foreach (var item in image._items)
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
                imageReduced = items.Count > 0 ? new CvImage(items) : CvImage.GenEmptyObj();
                return;
            }

            var mask = region.Mask;

            // 快速路径：单图像
            if (image.Count == 1)
            {
                var masked = new Mat();
                image.Image.CopyTo(masked, mask);
                imageReduced = new CvImage(masked, ownsImage: true);
                return;
            }

            // 多图像路径
            var multiItems = new List<ImageItem>(image.Count);
            foreach (var item in image._items)
            {
                var masked = new Mat();
                item.Image.CopyTo(masked, mask);
                multiItems.Add(new ImageItem(masked, ownsImage: true));
            }
            imageReduced = new CvImage(multiItems);
        }

        /// <summary>
        /// 裁剪定义域 - 对应 Halcon crop_domain(Image, ImageCropped)
        /// 裁剪到 Region BBox，改变图像尺寸（性能优化：在更小图像上处理）
        /// 支持 CvImage 数组：逐 item 裁剪到同一 BBox，输出等长 CvImage
        /// </summary>
        public static void CropDomain(CvImage image, CvRegion? region, out CvImage imageCropped, out Rect roiBBox)
        {
            // 快速路径：无 region
            if (region == null || region.IsEmpty)
            {
                var items = new List<ImageItem>(image.Count);
                foreach (var item in image._items)
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
                imageCropped = items.Count > 0 ? new CvImage(items) : CvImage.GenEmptyObj();
                roiBBox = image.Count > 0 ? new Rect(0, 0, image.Size.Width, image.Size.Height) : new Rect();
                return;
            }

            var mask = region.Mask;
            var bbox = Cv2.BoundingRect(mask);

            if (bbox.Width <= 0 || bbox.Height <= 0)
            {
                var items = new List<ImageItem>(image.Count);
                foreach (var item in image._items)
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
                imageCropped = items.Count > 0 ? new CvImage(items) : CvImage.GenEmptyObj();
                roiBBox = image.Count > 0 ? new Rect(0, 0, image.Size.Width, image.Size.Height) : new Rect();
                return;
            }

            // 裁剪掩码到 BBox（所有图像共用同一掩码）
            using var croppedMask = new Mat(mask, bbox);

            // 快速路径：单图像
            if (image.Count == 1)
            {
                using var croppedImage = new Mat(image.Image, bbox);
                var masked = new Mat();
                croppedImage.CopyTo(masked, croppedMask);
                imageCropped = new CvImage(masked, ownsImage: true);
                roiBBox = bbox;
                return;
            }

            // 多图像路径
            var multiItems = new List<ImageItem>(image.Count);
            foreach (var item in image._items)
            {
                using var croppedImage = new Mat(item.Image, bbox);
                var masked = new Mat();
                croppedImage.CopyTo(masked, croppedMask);
                multiItems.Add(new ImageItem(masked, ownsImage: true));
            }
            imageCropped = new CvImage(multiItems);
            roiBBox = bbox;
        }

        #endregion

        #region ✂️ 阈值分割

        /// <summary>
        /// 阈值分割 - 对应 Halcon threshold(Image, Region, Min, Max)
        /// </summary>
        public static void Threshold(
            CvImage image, CvRegion? region,
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
        /// 自适应阈值分割 - 对应 Halcon adaptive_threshold(Image, Region, ...)
        /// </summary>
        public static void AdaptiveThreshold(
            CvImage image, CvRegion? region,
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

        /// <summary>
        /// 灰度范围阈值 - 对应 Halcon threshold(Image, Region, MinGray, MaxGray)
        /// 选择 [MinGray, MaxGray] 范围内的像素（使用 InRange，对齐 Halcon 语义）
        /// 输出 CvRegion（Count=1，单区间）
        /// </summary>
        public static void ThresholdRange(
            CvImage image, CvRegion? region,
            int minGray, int maxGray,
            out CvRegion regionResult)
        {
            ThresholdRange(image, region, new[] { minGray }, new[] { maxGray }, out regionResult);
        }

        /// <summary>
        /// 多区间灰度范围阈值 - 对应 Halcon threshold(Image, Region, [MinGray1, MinGray2, ...], [MaxGray1, MaxGray2, ...])
        /// 每个区间 [minGray[i], maxGray[i]] 独立阈值，输出 N 个 RegionItem（Count = minGray.Length）
        /// 典型用法：ThresholdRange(image, null, new[]{0, 200}, new[]{100, 255}, out result) → 选择暗区+亮区
        /// </summary>
        public static void ThresholdRange(
            CvImage image, CvRegion? region,
            int[] minGray, int[] maxGray,
            out CvRegion regionResult)
        {
            if (minGray == null || maxGray == null)
                throw new ArgumentNullException(minGray == null ? nameof(minGray) : nameof(maxGray));
            if (minGray.Length != maxGray.Length)
                throw new ArgumentException("minGray 和 maxGray 长度必须相等");
            if (minGray.Length == 0)
            {
                regionResult = CvRegion.GenEmptyObj();
                return;
            }

            var items = new List<RegionItem>();
            var imageSize = image.Size;

            for (int i = 0; i < minGray.Length; i++)
            {
                var binaryMask = new Mat();
                Cv2.InRange(image.Image,
                    new Scalar(minGray[i]),
                    new Scalar(maxGray[i]),
                    binaryMask);

                // 应用 region 掩码限制
                if (region != null && !region.IsEmpty)
                {
                    var regionMask = region.Mask;
                    if (regionMask != null && !regionMask.Empty())
                    {
                        var masked = new Mat();
                        Cv2.BitwiseAnd(binaryMask, binaryMask, masked, regionMask);
                        binaryMask.Dispose();
                        binaryMask = masked;
                    }
                }

                if (Cv2.CountNonZero(binaryMask) > 0)
                    items.Add(new RegionItem(binaryMask));  // 转移所有权
                else
                    binaryMask.Dispose();
            }

            regionResult = items.Count > 0
                ? new CvRegion(items, imageSize)
                : CvRegion.GenEmptyObj();
        }

        #endregion

        #region 📊 统计

        /// <summary>
        /// 强度统计 - 对应 Halcon intensity(Image, Regions, Mean, Deviation)
        /// </summary>
        public static void Intensity(
            CvImage image, CvRegion? region,
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

        /// <summary>
        /// 多区域强度统计 - 对应 Halcon intensity(Images, Regions, Mean, Deviation)
        /// 逐 item 统计灰度均值和标准差，输出长度 = region.Count 的数组
        /// </summary>
        public static void Intensity(
            CvImage image, CvRegion region,
            out double[] meanValues, out double[] stdDevValues)
        {
            if (region == null || region.IsEmpty)
            {
                meanValues = Array.Empty<double>();
                stdDevValues = Array.Empty<double>();
                return;
            }

            int n = region.Count;
            meanValues = new double[n];
            stdDevValues = new double[n];

            var mat = image.Image;
            for (int i = 0; i < n; i++)
            {
                var itemMask = region._items[i].GetMask();
                using var meanMat = new Mat();
                using var stdDevMat = new Mat();
                Cv2.MeanStdDev(mat, meanMat, stdDevMat, itemMask);
                meanValues[i] = meanMat.Get<double>(0);
                stdDevValues[i] = stdDevMat.Get<double>(0);
            }
        }

        #endregion

        #region 🖼️ 显示

        /// <summary>
        /// 从区域提取掩码图像 - 对应 Halcon region_to_bin(Region : : : BinaryImage)
        /// </summary>
        public static void RegionToMask(CvRegion region, out Mat mask)
        {
            mask = region.Mask?.Clone() ?? new Mat(region.ImageSize, MatType.CV_8UC1, Scalar.Black);
        }

        /// <summary>
        /// 组合显示图像 - 对应 Halcon dev_display 组合效果
        /// </summary>
        public static void ComposeDisplay(
            CvImage original, CvRegion? region, CvRegion binaryRegion,
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
        /// 产出 N 个 RegionItem（Mask 懒加载）+ CC 缓存，下游算子零开销复用
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

            // 创建 RegionItem 列表（Mask 懒加载，从 _ccLabels 按需提取）
            var items = new List<RegionItem>(numLabels - 1);
            for (int i = 1; i < numLabels; i++)
                items.Add(new RegionItem(labels, i, region.ImageSize));

            // 转移所有权给 CvRegion（不用 using）
            connectedRegions = new CvRegion(items, region.ImageSize, labels, stats, centroids);
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
        /// 复用已有 CC 缓存（零 CC 开销），输出保留 items + CC 缓存（过滤后重编号）
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
                    // 矩形度直接从 stats 计算（零 FindContours 开销）
                    double rect = ComputeRectangularityFromStats(stats, i);
                    if (rect < minRectangularity || rect > maxRectangularity) continue;

                    // 圆度需要 FindContours，仅在有圆度筛选时才计算
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

            // 重建 labels（过滤 + 重编号 1..M），使下游继续受益于缓存
            int M = filteredIndices.Count;
            var resultLabels = BuildFilteredLabels(labels, filteredIndices, region.ImageSize);
            var resultStats = FilterCCRows(stats, filteredIndices, 5, MatType.CV_32SC1);

            Mat resultCentroids;
            if (region.Centroids != null && !region.Centroids.Empty())
                resultCentroids = FilterCCRows(region.Centroids!, filteredIndices, 2, MatType.CV_64FC1);
            else
                resultCentroids = new Mat();

            // 创建过滤后的 RegionItem 列表（懒加载引用新的 resultLabels）
            var resultItems = new List<RegionItem>(M);
            for (int i = 0; i < M; i++)
                resultItems.Add(new RegionItem(resultLabels, i + 1, region.ImageSize));

            selected = new CvRegion(resultItems, region.ImageSize, resultLabels, resultStats, resultCentroids);

            if (ownsTempMats) { labels.Dispose(); stats.Dispose(); }
        }

        /// <summary>
        /// 将多区域拆分为单区域数组 - 对应 Halcon connection + 遍历子区域
        /// 逐 item 提取 Mask，构造独立 CvRegion
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
        /// <param name="region">多区域对象</param>
        /// <param name="index">子区域索引（0-based，范围 0..Count-1）</param>
        /// <param name="singleRegion">选中的单个子区域</param>
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

        /// <summary>
        /// 多区域特征查询 - 对应 Halcon region_features(Regions, Features, Values)
        /// 优先复用已缓存的 stats（零 CC 开销），每个特征输出一维数组
        /// </summary>
        /// <param name="region">多区域对象（需有 labels/stats 缓存）</param>
        /// <param name="features">要查询的特征列表</param>
        /// <param name="values">输出：N 个一维数组，每个数组对应一个特征，长度 = 子区域数</param>
        public static void RegionFeatures(CvRegion region, RegionFeature[] features, out double[][] values)
        {
            if (features == null || features.Length == 0 || region.IsEmpty)
            {
                values = Array.Empty<double[]>();
                return;
            }

            int n = region.Count;
            values = new double[features.Length][];

            // 快速路径：复用已缓存的 stats
            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var stats = region.Stats;
                var centroids = region.Centroids;

                for (int fi = 0; fi < features.Length; fi++)
                {
                    values[fi] = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        int labelIdx = i + 1; // 0=背景, 1..N=子区域
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
                            RegionFeature.Rectangularity =>
                                ComputeRectangularityFromStats(stats, labelIdx),
                            _ => 0 // 其他特征需要逐区域计算，此处返回0
                        };
                    }
                }
                return;
            }

            // 慢速路径：无缓存，Split 后逐区域查询
            Split(region, out var splitRegions);
            try
            {
                for (int fi = 0; fi < features.Length; fi++)
                {
                    values[fi] = new double[n];
                }
                for (int i = 0; i < splitRegions.Length && i < n; i++)
                {
                    RegionFeatures(splitRegions[i], out var singleValues, features);
                    for (int fi = 0; fi < features.Length; fi++)
                    {
                        values[fi][i] = singleValues[fi];
                    }
                }
            }
            finally
            {
                foreach (var r in splitRegions) r.Dispose();
            }
        }

        /// <summary>
        /// 最小外接矩形1 - 对应 Halcon smallest_rectangle1(Regions, Row1, Col1, Row2, Col2)
        /// 优先复用已缓存的 stats（零 CC 开销）
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

            // 快速路径：复用已缓存的 stats
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

            // 慢速路径：无缓存时
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
        /// 优先复用已缓存的 stats（零 CC 开销）
        /// </summary>
        public static void Rectangularity(CvRegion region, out double[] rectangularity)
        {
            if (region.IsEmpty || region.Count == 0)
            {
                rectangularity = Array.Empty<double>();
                return;
            }

            // 快速路径：复用已缓存的 stats
            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var stats = region.Stats;
                int n = region.Count;
                rectangularity = new double[n];

                for (int i = 0; i < n; i++)
                    rectangularity[i] = ComputeRectangularityFromStats(stats, i + 1);

                return;
            }

            // 慢速路径：无缓存时
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
        /// 快速路径：有 CC 缓存时从 Stats 读 BBox + 裁剪 Labels ROI + 小区域 Compare
        /// 慢速路径：逐 item GetMask + BoundingRect + FindContours
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

            // 快速路径：有 CC 缓存 → 从 Stats 读 BBox + 裁剪 Labels ROI + 小区域 Compare
            if (region.HasLabels && region.Stats != null && !region.Stats.Empty())
            {
                var labels = region.Labels!;
                var stats = region.Stats!;

                for (int i = 0; i < n; i++)
                {
                    int labelIdx = i + 1;
                    int left   = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Left);
                    int top    = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Top);
                    int width  = stats.At<int>(labelIdx, (int)ConnectedComponentsTypes.Width);
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
                    {
                        contours[i] = CvContour.GenEmptyObj();
                    }
                }
                return;
            }

            // 慢速路径：无 CC 缓存（非 Connection 产出的区域），逐 item GetMask
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
                {
                    contours[i] = CvContour.GenEmptyObj();
                }
            }
        }

        /// <summary>
        /// 轮廓圆度 - 对应 Halcon circularity(XLDContours, Circularity)
        /// 公式: 4πA / P² （0~1, 1=完美圆）
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

                // 4πA / P² （0~1, 1=完美圆）
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

        #region 🔧 集合运算

        /// <summary>区域并集 - 对应 Halcon union1(Regions : : : RegionUnion)</summary>
        public static void Union1(CvRegion region, out CvRegion unionRegion)
        {
            if (region.IsEmpty)
            {
                unionRegion = CvRegion.GenEmptyObj();
                return;
            }
            var combinedMask = region.Mask;
            unionRegion = new CvRegion(combinedMask?.Clone(), region.ImageSize);
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
                resultDifference = new CvRegion(sub.Mask?.Clone(), sub.ImageSize);
                return;
            }

            var subMask = sub.Mask!;
            var patternMask = pattern.Mask!;

            using var notPattern = new Mat();
            Cv2.BitwiseNot(patternMask, notPattern);

            var resultMask = new Mat();
            Cv2.BitwiseAnd(subMask, notPattern, resultMask);
            resultDifference = new CvRegion(resultMask, sub.ImageSize);
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
            Cv2.BitwiseAnd(region1.Mask!, region2.Mask!, resultMask);
            resultIntersection = new CvRegion(resultMask, region1.ImageSize);
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
            Cv2.BitwiseNot(region.Mask!, resultMask);
            resultComplement = new CvRegion(resultMask, region.ImageSize);
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
                concatResult = region.CopyObj();
                return;
            }
            if (region.IsEmpty)
            {
                concatResult = other.CopyObj();
                return;
            }

            // 合并掩码 + 合并 items
            var resultMask = new Mat();
            Cv2.BitwiseOr(region.Mask!, other.Mask!, resultMask);

            // 拷贝两边的 items（强制提取 Mask 以断开懒加载引用）
            var items = new List<RegionItem>(region.Count + other.Count);
            foreach (var item in region._items)
            {
                var m = item.GetMask();
                items.Add(m != null && !m.Empty() ? new RegionItem(m.Clone()) : new RegionItem(new Mat()));
            }
            foreach (var item in other._items)
            {
                var m = item.GetMask();
                items.Add(m != null && !m.Empty() ? new RegionItem(m.Clone()) : new RegionItem(new Mat()));
            }

            concatResult = new CvRegion(items, region.ImageSize);
        }

        #endregion

        #region 🎛️ 图像滤波

        /// <summary>
        /// 均值滤波 - 对应 Halcon mean_image
        /// </summary>
        public static void MeanImage(CvImage image, int kernelWidth, int kernelHeight,
            out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
        {
            if (image.Empty)
            {
                filteredImage = CvImage.GenEmptyObj();
                return;
            }

            var result = new Mat();
            Cv2.Blur(image.Image, result, new Size(kernelWidth, kernelHeight),
                new Point(-1, -1), borderType);
            filteredImage = new CvImage(result, ownsImage: true);
        }

        /// <summary>
        /// 高斯滤波 - 对应 Halcon gauss_image
        /// </summary>
        public static void GaussImage(CvImage image, int kernelWidth, int kernelHeight,
            double sigmaX, double sigmaY,
            out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
        {
            if (image.Empty)
            {
                filteredImage = CvImage.GenEmptyObj();
                return;
            }

            var result = new Mat();
            Cv2.GaussianBlur(image.Image, result,
                new Size(kernelWidth, kernelHeight), sigmaX, sigmaY, borderType);
            filteredImage = new CvImage(result, ownsImage: true);
        }

        /// <summary>
        /// 中值滤波 - 对应 Halcon median_image
        /// </summary>
        public static void MedianImage(CvImage image, int kernelSize,
            out CvImage filteredImage)
        {
            if (image.Empty)
            {
                filteredImage = CvImage.GenEmptyObj();
                return;
            }

            var result = new Mat();
            Cv2.MedianBlur(image.Image, result, kernelSize);
            filteredImage = new CvImage(result, ownsImage: true);
        }

        /// <summary>
        /// 双边滤波 - 对应 Halcon bilateral_filter
        /// </summary>
        public static void BilateralFilterImage(CvImage image, int diameter,
            double sigmaColor, double sigmaSpace,
            out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
        {
            if (image.Empty)
            {
                filteredImage = CvImage.GenEmptyObj();
                return;
            }

            var result = new Mat();
            Cv2.BilateralFilter(image.Image, result, diameter, sigmaColor, sigmaSpace, borderType);
            filteredImage = new CvImage(result, ownsImage: true);
        }

        /// <summary>
        /// 自定义卷积滤波 - 对应 Halcon convol_image
        /// </summary>
        public static void ConvolImage(CvImage image, Mat kernel,
            out CvImage filteredImage, BorderTypes borderType = BorderTypes.Reflect101)
        {
            if (image.Empty)
            {
                filteredImage = CvImage.GenEmptyObj();
                return;
            }

            var result = new Mat();
            Cv2.Filter2D(image.Image, result, -1, kernel, new Point(-1, -1), 0, borderType);
            filteredImage = new CvImage(result, ownsImage: true);
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
                dilatedRegion = new CvRegion(dilated, region.ImageSize);
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
                erodedRegion = new CvRegion(eroded, region.ImageSize);
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
                dilatedRegion = new CvRegion(dilated, region.ImageSize);
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
                erodedRegion = new CvRegion(eroded, region.ImageSize);
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

        /// <summary>
        /// 形态学操作（Mat版）- 直接操作二值图像
        /// 支持所有 MorphTypes: Erode/Dilate/Open/Close/Gradient/TopHat/BlackHat
        /// 支持所有 MorphShapes: Rect/Cross/Ellipse
        /// </summary>
        public static void MorphologyEx(
            Mat input, out Mat result,
            MorphTypes op,
            MorphShapes kernelShape,
            int kernelWidth, int kernelHeight,
            int iterations = 1,
            BorderTypes borderType = BorderTypes.Constant,
            Scalar? borderValue = null)
        {
            var kernel = Cv2.GetStructuringElement(kernelShape,
                new Size(kernelWidth, kernelHeight));
            try
            {
                result = new Mat();
                if (op == MorphTypes.Erode || op == MorphTypes.Dilate)
                {
                    if (op == MorphTypes.Erode)
                        Cv2.Erode(input, result, kernel, new Point(-1, -1), iterations, borderType,
                            borderValue ?? Scalar.All(255));
                    else
                        Cv2.Dilate(input, result, kernel, new Point(-1, -1), iterations, borderType,
                            borderValue ?? Scalar.All(255));
                }
                else
                {
                    Cv2.MorphologyEx(input, result, op, kernel, new Point(-1, -1), iterations, borderType,
                        borderValue ?? Scalar.All(255));
                }
            }
            finally
            {
                kernel.Dispose();
            }
        }

        /// <summary>
        /// 形态学操作（CvImage版）
        /// </summary>
        public static void MorphologyEx(
            CvImage image, out CvImage resultImage,
            MorphTypes op,
            MorphShapes kernelShape,
            int kernelWidth, int kernelHeight,
            int iterations = 1,
            BorderTypes borderType = BorderTypes.Constant,
            Scalar? borderValue = null)
        {
            if (image.Empty)
            {
                resultImage = CvImage.GenEmptyObj();
                return;
            }

            MorphologyEx(image.Image, out var result,
                op, kernelShape, kernelWidth, kernelHeight,
                iterations, borderType, borderValue);
            resultImage = new CvImage(result, ownsImage: true);
        }

        #endregion

        #region 🆕 形状匹配 - 梯度引擎（v3.0 新增）

        /// <summary>形状匹配边缘点 — 对齐 Halcon ModelID 内部结构</summary>
        public class EdgePoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Direction { get; set; }  // 弧度 [-π, π], atan2(Gy, Gx)
            public double Magnitude { get; set; }  // 梯度幅值 sqrt(Gx²+Gy²)
        }

        // ==================== 共享基础梯度（模板创建 + 运行时 100% 相同）====================

        /// <summary>
        /// 形状匹配专用梯度提取 — Scharr 3×3 固定算子
        /// 模板创建和运行时匹配完全相同的调用
        /// 不使用 GaussianBlur 预处理
        /// </summary>
        public static void ComputeShapeGradient(Mat grayImage, out Mat gx, out Mat gy, out Mat mag)
        {
            gx = new Mat();
            gy = new Mat();
            Cv2.Scharr(grayImage, gx, MatType.CV_32F, 1, 0);
            Cv2.Scharr(grayImage, gy, MatType.CV_32F, 0, 1);
            mag = new Mat();
            Cv2.Magnitude(gx, gy, mag);

            // 归一化到 [0, 255]，对齐 VisionPro/Halcon 的对比度语义
            // Scharr 系数和=16，8bit 图像最大梯度≈16×255=4080，归一化后：
            // 微弱边缘~5，中等~50，强边缘~150+，极强~255
            Cv2.MinMaxLoc(mag, out _, out double maxVal);
            if (maxVal > 1e-6f)
            {
                double scale = 255.0 / maxVal;
                mag.ConvertTo(mag, MatType.CV_32F, scale);
            }
        }

        // ==================== 非极大值抑制（细线化到 1 像素）====================

        /// <summary>
        /// 非极大值抑制 — 沿梯度方向保留局部极大值，细线化到 1 像素
        /// 使用双线性插值沿精确梯度方向采样，替代传统 4 方向离散比较
        /// 等价于 Halcon 的 edges_sub_pix 细线化步骤（不含边缘提取）
        /// </summary>
        public static Mat ApplyNms(Mat mag, Mat gx, Mat gy, float contrast)
        {
            int w = mag.Width, h = mag.Height;
            var nms = new Mat(h, w, MatType.CV_32F, Scalar.All(0f));
            int total = w * h;
            float[] nmsData = new float[total];
            float[] magData = new float[total];
            float[] gxData = new float[total];
            float[] gyData = new float[total];
            System.Runtime.InteropServices.Marshal.Copy(mag.Data, magData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gx.Data, gxData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gy.Data, gyData, 0, total);

            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int idx = y * w + x;
                    float m = magData[idx];
                    float lowThresh = Math.Max(contrast, 1f);
                    if (m < lowThresh) continue;

                    float gxVal = gxData[idx];
                    float gyVal = gyData[idx];

                    // ★ 双线性插值 NMS：沿精确梯度方向采样，不量化到 4 个固定方向
                    double angle = Math.Atan2(gyVal, gxVal);
                    double dx = Math.Cos(angle);
                    double dy = Math.Sin(angle);

                    float m1 = BilinearInterpolate(magData, w, h, x + dx, y + dy);
                    float m2 = BilinearInterpolate(magData, w, h, x - dx, y - dy);

                    if (m >= m1 && m >= m2)
                        nmsData[idx] = m;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(nmsData, 0, nms.Data, nmsData.Length);
            return nms;
        }

        /// <summary>
        /// 双线性插值 — 在浮点坐标处采样 float 数组值
        /// 越界时返回 0（等效于零填充）
        /// </summary>
        private static float BilinearInterpolate(float[] data, int w, int h, double fx, double fy)
        {
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            // 边界钳位：越界返回 0
            if (x0 < 0 || x0 >= w || y0 < 0 || y0 >= h) return 0f;
            if (x1 < 0 || x1 >= w || y1 < 0 || y1 >= h) return 0f;

            double wx = fx - Math.Floor(fx);
            double wy = fy - Math.Floor(fy);

            return (float)(
                data[y0 * w + x0] * (1 - wx) * (1 - wy) +
                data[y0 * w + x1] * wx * (1 - wy) +
                data[y1 * w + x0] * (1 - wx) * wy +
                data[y1 * w + x1] * wx * wy
            );
        }


        // ==================== 收集边缘点（统一 FindContours 提取 + 长度变动率断崖链长过滤）====================

        /// <summary>
        /// 从 NMS 结果中收集所有边缘点
        /// 使用 FindContours 统一完成提取和链长过滤，消除双系统语义不一致
        /// Auto 模式下使用长度变动率断崖算法自适应计算链长阈值
        /// </summary>
        public static List<EdgePoint> CollectEdgePoints(Mat nms, Mat gx, Mat gy,
            bool autoMinChainLength, int manualMinChainLength, out int actualChainLength,
            int closingRadius = 0)
        {
            actualChainLength = 0;
            int total = (int)nms.Total();
            int w = nms.Width;

            // 1. NMS → 二值图
            using var binaryNms = new Mat();
            Cv2.Threshold(nms, binaryNms, 0.001, 255, ThresholdTypes.Binary);
            binaryNms.ConvertTo(binaryNms, MatType.CV_8U);

            // ★ 2. 闭运算 — 桥接 NMS 断裂的弱边缘碎片（默认不启用）
            //     原理：先膨胀（连接相邻碎片）→ 再腐蚀（恢复原细度）
            //     radius=1 → 3×3 椭圆核，桥接间隔 ≤1 个像素的断裂点
            if (closingRadius > 0)
            {
                using var kernel = Cv2.GetStructuringElement(
                    MorphShapes.Ellipse, new Size(2 * closingRadius + 1, 2 * closingRadius + 1));
                Cv2.MorphologyEx(binaryNms, binaryNms, MorphTypes.Close, kernel);
            }

            // 3. FindContours 获取所有独立轮廓
            Cv2.FindContours(binaryNms, out Point[][] contours, out _,
                RetrievalModes.List, ContourApproximationModes.ApproxNone);

            if (contours.Length == 0) return new List<EdgePoint>();

            // 4. 提取各轮廓链长并计算阈值
            int[] chainLengths = contours.Select(c => c.Length).ToArray();
            int threshold = autoMinChainLength
                ? ComputeCliffThreshold(chainLengths)
                : manualMinChainLength;
            actualChainLength = threshold;

            // 5. 读取梯度数据用于构造 EdgePoint
            float[] nmsData = new float[total];
            float[] gxData = new float[total];
            float[] gyData = new float[total];
            System.Runtime.InteropServices.Marshal.Copy(nms.Data, nmsData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gx.Data, gxData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gy.Data, gyData, 0, total);

            // 6. 从满足链长条件的轮廓中构造 EdgePoint
            var result = new List<EdgePoint>();
            for (int i = 0; i < contours.Length; i++)
            {
                if (chainLengths[i] >= threshold)
                {
                    foreach (Point pt in contours[i])
                    {
                        int idx = pt.Y * w + pt.X;
                        result.Add(new EdgePoint
                        {
                            X = pt.X,
                            Y = pt.Y,
                            Direction = Math.Atan2(gyData[idx], gxData[idx]),
                            Magnitude = nmsData[idx]
                        });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 空间均匀散布选取特征点 — 替代 Top25% 幅值截断
        /// 按幅值降序排列后，逐点检查与已选点是否距离过近，确保特征点在空间上均匀散布
        /// </summary>
        /// <param name="candidates">候选边缘点列表</param>
        /// <param name="maxPoints">最多选取点数</param>
        /// <param name="minDist">最小间距（像素），默认 3.0</param>
        public static List<EdgePoint> SelectScatteredFeatures(
            List<EdgePoint> candidates, int maxPoints, float minDist = 3.0f)
        {
            if (candidates.Count <= maxPoints) return candidates;

            var sorted = candidates.OrderByDescending(p => p.Magnitude).ToList();
            var selected = new List<EdgePoint>(maxPoints) { sorted[0] };
            float minDistSq = minDist * minDist;

            foreach (var pt in sorted.Skip(1))
            {
                if (selected.Count >= maxPoints) break;
                bool tooClose = false;
                foreach (var sel in selected)
                {
                    float dx = (float)(pt.X - sel.X);
                    float dy = (float)(pt.Y - sel.Y);
                    if (dx * dx + dy * dy < minDistSq) { tooClose = true; break; }
                }
                if (!tooClose) selected.Add(pt);
            }

            // 后备：不够则按幅值补满
            if (selected.Count < maxPoints)
            {
                foreach (var pt in sorted)
                {
                    if (selected.Count >= maxPoints) break;
                    if (!selected.Contains(pt)) selected.Add(pt);
                }
            }
            return selected;
        }

        /// <summary>
        /// 取唯一链长升序排序后 20% 分位值作为链长阈值
        ///
        /// 全部链长参与去重排序，取 20% 分位值过滤最短的噪声轮廓。
        /// 不需要预过滤碎片噪声，也不需要复杂的断崖检测策略。
        ///
        /// 示例：
        ///   连续分布 [50, 51, ..., 210] (Count=161)
        ///   → index=32, unique[32]=82 ← 过滤最短 32 种链长
        /// </summary>
        private static int ComputeCliffThreshold(int[] chainLengths)
        {
            var unique = chainLengths.Distinct().OrderBy(v => v).ToList();
            if (unique.Count <= 1) return unique[0];
            int index = (int)(unique.Count * 0.2);
            return unique[index];
        }

        /// <summary>
        /// 从梯度幅值图自动计算对比度阈值（Otsu 最大类间方差法）
        ///
        /// 算法：
        ///   1. 构建梯度幅值直方图 [0, 255]
        ///   2. 只对非零像素（hist[1..255]）执行 Otsu 二值化
        ///   3. 寻找使类间方差 σ² = ω₀·ω₁·(μ₀-μ₁)² 最大的阈值
        ///   4. 下界 3 压制噪声，上界 250 避免全边缘图像过分割
        ///
        /// 优势：自适应分割弱纹理与强边缘，不依赖固定百分位参数
        /// 前提：mag 应先归一化到 [0, 255]（ComputeShapeGradient 已内置归一化）
        /// </summary>
        public static float AutoComputeContrast(Mat mag)
        {
            int total = (int)mag.Total();
            if (total == 0) return 30f;

            float[] data = new float[total];
            System.Runtime.InteropServices.Marshal.Copy(mag.Data, data, 0, total);

            // 构建梯度幅值直方图 [0, 255]，四舍五入分桶
            int[] hist = new int[256];
            int nonZeroCount = 0;
            float dataMin = 255f, dataMax = 0f;
            for (int i = 0; i < total; i++)
            {
                int bin = Math.Min(255, (int)Math.Round(data[i], MidpointRounding.AwayFromZero));
                if (bin > 0)
                {
                    nonZeroCount++;
                    if (data[i] > dataMax) dataMax = data[i];
                    if (data[i] < dataMin) dataMin = data[i];
                }
                hist[bin]++;
            }

            if (nonZeroCount < 10)
            {
                VisionLogger.Instance.Warning(
                    $"AutoComputeContrast: 非零像素不足——nonZeroCount={nonZeroCount} < 10，返回 3。 " +
                    $"total={total}, minNonZero={dataMin:F2}, max={dataMax:F2}",
                    "CvOp");
                return 3f;
            }

            // Otsu 最大类间方差法（仅对非零像素 hist[1..255]）
            double globalMean = 0;
            for (int b = 1; b < 256; b++)
                globalMean += (double)b * hist[b];
            globalMean /= nonZeroCount;

            double maxVariance = -1;
            int bestThreshold = 1;
            int sumB = 0;           // 前 t 个 bin 累计像素数
            double sumBWeighted = 0; // 前 t 个 bin 累计加权和

            for (int t = 1; t < 255; t++)
            {
                sumB += hist[t];
                if (sumB == 0) continue;           // 空类，跳过
                sumBWeighted += (double)t * hist[t];

                double w0 = (double)sumB / nonZeroCount;  // 前景占比
                double w1 = 1.0 - w0;                      // 背景占比
                double mu0 = sumBWeighted / sumB;           // 前景均值
                double mu1 = (globalMean - w0 * mu0) / w1;  // 背景均值
                double variance = w0 * w1 * (mu0 - mu1) * (mu0 - mu1);

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    bestThreshold = t;
                }
            }

            // 钳位到 [3, 250]
            float result = Math.Max(3f, Math.Min(250f, (float)bestThreshold));
            VisionLogger.Instance.Info(
                $"AutoComputeContrast: Otsu——bestThreshold={bestThreshold}, 钳位后={result:F1}。 " +
                $"total={total}, nonZeroCount={nonZeroCount}, minNonZero={dataMin:F2}, max={dataMax:F2}",
                "CvOp");
            return result;
        }

        #endregion

        #region 🔍 形状匹配 - 响应图引擎

        /// <summary>
        /// 形状匹配 - 响应图查表引擎（使用每层独立边缘点金字塔）
        /// </summary>
        public static void FindShape(
            Mat grayImage,
            Dictionary<int, List<EdgePoint>> pyramidEdgePoints,
            double minScore,
            double angleStart, double angleEnd, double angleStep,
            double scaleMin, double scaleMax, double scaleStep,
            int pyramidStart,
            int pyramidEnd,
            int timeoutMs,
            out List<ShapeMatchResult> matchResults,
            out MatchPerformanceStats perfStats)
        {
            matchResults = new List<ShapeMatchResult>();
            perfStats = new MatchPerformanceStats
            {
                TotalTemplatePoints = pyramidEdgePoints?.GetValueOrDefault(0)?.Count ?? 0
            };

            if (grayImage == null || grayImage.Empty()) return;
            if (pyramidEdgePoints == null || pyramidEdgePoints.Count == 0) return;

            var overallSw = Stopwatch.StartNew();

            // ===== Step 1: 确定建造层数 =====
            int buildLevels = pyramidStart + 1;

            // ===== Step 2: 构建响应图金字塔（跳过 < pyramidEnd 的层级） =====
            var buildSw = Stopwatch.StartNew();
            var pyramid = BuildResponsePyramid(grayImage, buildLevels, pyramidEnd);
            buildSw.Stop();
            perfStats.BuildPyramidMs = buildSw.ElapsedMilliseconds;
            perfStats.PyramidLevelCount = pyramid.Count;

            // ★ 坐标监控：记录金字塔各层实际尺寸和缩放比
            foreach (var lvlData in pyramid)
            {
                int lvlIdx = pyramid.IndexOf(lvlData);
                if (lvlData.ResponseMaps != null)
                {
                    perfStats.CoordinateChainLog.Add(
                        $"  [PyramidLv{lvlIdx}] 响应图尺寸={lvlData.Width}x{lvlData.Height}, " +
                        $"ScaleX={lvlData.ScaleX:F6}, ScaleY={lvlData.ScaleY:F6}");
                }
                else
                {
                    perfStats.CoordinateChainLog.Add(
                        $"  [PyramidLv{lvlIdx}](跳过) 尺寸={lvlData.Width}x{lvlData.Height}, " +
                        $"ScaleX={lvlData.ScaleX:F6}, ScaleY={lvlData.ScaleY:F6}");
                }
            }

            int actualMaxLevel = Math.Min(pyramidStart, pyramid.Count - 1);

            // ===== Step 3: 由粗到精搜索 =====
            List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)>? prevCandidates = null;

            for (int level = actualMaxLevel; level >= pyramidEnd; level--)
            {
                if (overallSw.ElapsedMilliseconds > timeoutMs) break;

                var levelData = pyramid[level];
                if (levelData.ResponseMaps == null) continue;

                bool isCoarsest = (level == actualMaxLevel);
                bool isFinest = (level == pyramidEnd);

                // ★ 每层使用该层独立提取的边缘点（非全分辨率几何缩放）
                if (!pyramidEdgePoints.TryGetValue(level, out var levelPoints))
                    throw new InvalidOperationException(
                        $"FindShape: 金字塔 Level {level} 边缘点数据缺失" +
                        $" (范围: {pyramidEnd}~{pyramidStart})");

                if (levelPoints.Count < 3)
                {
                    prevCandidates = null;
                    continue;
                }

                // ★ 粗层边缘点降采样 — 使用空间均匀散布选取替代 Top25% 截断
                int maxPointsForLevel = level switch
                {
                    4 => 80,
                    3 => 150,
                    2 => 300,
                    1 => 600,
                    _ => 0  // Level 0: 0 = 按百分比自动截断
                };
                int autoCap = maxPointsForLevel == 0
                    ? Math.Max(levelPoints.Count / 4, 400)
                    : maxPointsForLevel;
                if (levelPoints.Count > autoCap)
                {
                    levelPoints = SelectScatteredFeatures(levelPoints, autoCap);
                }

                byte[] quantizedDirs = QuantizeEdgePointsDirections(levelPoints);
                int tmplCount = levelPoints.Count;

                // ★ 从该层点集提取归一化轮廓（所有层级已预归一化到统一参考点，坐标为相对参考点的偏移）
                var normalizedContour = levelPoints
                    .Select(p => new Point2f((float)p.X, (float)p.Y))
                    .ToList();

                var candidates = new List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)>();
                bool isFullSearch = (prevCandidates == null || prevCandidates.Count == 0);

                var searchSw = Stopwatch.StartNew();
                var searchStats = new SearchStats();

                if (isFullSearch)
                {
                    double coarseMinScore = Math.Max(minScore * 0.5, 0.1);
                    double coarseAngleStep = Math.Max(angleStep * (isCoarsest ? 3 : 2), 5.0);
                    double coarseScaleStep = Math.Max(scaleStep * (isCoarsest ? 4 : 2), 0.02);

                    FullSearchOnResponseMaps(levelData.ResponseMaps, levelData.Width, levelData.Height,
                        normalizedContour, quantizedDirs,
                        coarseMinScore, angleStart, angleEnd, coarseAngleStep,
                        scaleMin, scaleMax, coarseScaleStep,
                        8, candidates, overallSw, timeoutMs,
                        ref searchStats);

                    // ★ P2: Cascade 退化保护 — 当前层无候选时以小步幅重试
                    if (candidates.Count == 0 && level > pyramidEnd)
                    {
                        double fallbackScore = Math.Max(coarseMinScore * 0.5, 0.05);
                        int finerStep = Math.Max(8 / 2, 4);
                        candidates.Clear();
                        var fallbackStats = new SearchStats();
                        FullSearchOnResponseMaps(levelData.ResponseMaps, levelData.Width, levelData.Height,
                            normalizedContour, quantizedDirs,
                            fallbackScore, angleStart, angleEnd, coarseAngleStep,
                            scaleMin, scaleMax, coarseScaleStep,
                            finerStep, candidates, overallSw, timeoutMs,
                            ref fallbackStats);
                        // 合并 fallback 统计
                        searchStats.ScaleCombosTried += fallbackStats.ScaleCombosTried;
                        searchStats.AngleCombosTried += fallbackStats.AngleCombosTried;
                        searchStats.SpatialPixelsSearched += fallbackStats.SpatialPixelsSearched;
                        searchStats.TotalTemplateChecks += fallbackStats.TotalTemplateChecks;
                        searchStats.EarlyTerminations += fallbackStats.EarlyTerminations;
                        searchStats.EarlyTerminationChecks += fallbackStats.EarlyTerminationChecks;
                        searchStats.CandidatesOut += fallbackStats.CandidatesOut;
                    }

                    // ★ P3: 级联终止 — 粗层 FullSearch(+Fallback) 仍无候选则无需更细层
                    if (candidates.Count == 0 && level > pyramidEnd)
                        break;
                }
                else
                {
                    double levelMinScore = isFinest ? minScore : minScore * 0.7;

                    // 取前一层（刚搜索过的粗层）的实际尺寸用于坐标映射
                    var prevLevelData = pyramid[level + 1];
                    RefineSearchOnResponseMaps(levelData.ResponseMaps, levelData.Width, levelData.Height,
                        normalizedContour, quantizedDirs,
                        levelMinScore, 2, candidates, prevCandidates,
                        angleStep, scaleStep, prevLevelData.Width, prevLevelData.Height, overallSw, timeoutMs,
                        ref searchStats);
                }

                searchSw.Stop();

                if (level > pyramidEnd && candidates.Count > 100)
                {
                    candidates = candidates.OrderByDescending(c => c.Score).Take(100).ToList();
                }

                // ★ 坐标监控：记录本层最优候选信息
                (double bestScore, int bestX, int bestY, double bestAng, double bestScl, _) =
                    candidates.Count > 0
                        ? candidates.OrderByDescending(c => c.Score).First()
                        : (0.0, 0, 0, 0.0, 0.0, 0);

                double? mappedPrevX = null, mappedPrevY = null;
                if (!isFullSearch && prevCandidates is { Count: > 0 })
                {
                    // 记录粗层最优候选映射到本层的位置（与 RefineSearch 内逻辑一致）
                    var prevLevelData = pyramid[level + 1];
                    double rX = (double)levelData.Width / prevLevelData.Width;
                    double rY = (double)levelData.Height / prevLevelData.Height;
                    mappedPrevX = prevCandidates.OrderByDescending(c => c.Score).First().X * rX;
                    mappedPrevY = prevCandidates.OrderByDescending(c => c.Score).First().Y * rY;
                }

                // ★ 记录本层性能统计 + 坐标信息
                var lvlStats = new MatchPerformanceStats.LevelStats
                {
                    Level = level,
                    ImageW = levelData.Width,
                    ImageH = levelData.Height,
                    ScaleX = levelData.ScaleX,
                    ScaleY = levelData.ScaleY,
                    TemplatePointsUsed = levelPoints.Count,
                    SearchMs = searchSw.ElapsedMilliseconds,
                    Candidates = candidates.Count,
                    IsFullSearch = isFullSearch,

                    // ★ 搜索过程性能明细
                    AngleCombosTried = searchStats.AngleCombosTried,
                    ScaleCombosTried = searchStats.ScaleCombosTried,
                    SpatialPixelsSearched = searchStats.SpatialPixelsSearched,
                    TotalTemplateChecks = searchStats.TotalTemplateChecks,
                    EarlyTerminations = searchStats.EarlyTerminations,
                    EarlyTerminationAvgPoint = searchStats.EarlyTerminationAvgPoint,

                    BestScore = bestScore,
                    BestX = bestX,
                    BestY = bestY,
                    BestAngle = bestAng,
                    BestScale = bestScl,
                    MappedFromPrevX = mappedPrevX,
                    MappedFromPrevY = mappedPrevY
                };

                if (isFinest)
                {
                    // 结果坐标在 level 的降采样空间，用实际缩放比缩放到 Level 0（全分辨率）坐标
                    // 避免硬编码 1 << level 导致的奇偶舍入误差
                    double scaleX = levelData.ScaleX;
                    double scaleY = levelData.ScaleY;
                    int totalPoints = levelPoints.Count;
                    foreach (var (score, x, y, ang, scl, matchedCount) in candidates)
                    {
                        matchResults.Add(new ShapeMatchResult
                        {
                            Score = score,
                            MatchRatio = totalPoints > 0 ? (double)matchedCount / totalPoints : 0,
                            CenterX = x * scaleX,
                            CenterY = y * scaleY,
                            Angle = ang,
                            Scale = scl
                        });
                    }

                    // ★ 坐标监控：记录最优候选映射到原图的最终坐标
                    if (candidates.Count > 0)
                    {
                        var best = candidates.OrderByDescending(c => c.Score).First();
                        lvlStats.FinalCenterX = best.X * scaleX;
                        lvlStats.FinalCenterY = best.Y * scaleY;
                    }

                    // ★ 坐标监控：输出本层坐标链路摘要
                    string refSrc = isFullSearch ? "FullSearch" : "Refine";
                    string coordInfo = $"  [Lv{level}]({refSrc}) " +
                        $"img={levelData.Width}x{levelData.Height}, " +
                        $"Scale=({scaleX:F6},{scaleY:F6}), " +
                        $"candidates={candidates.Count}, " +
                        $"best=({bestX},{bestY})@sc={bestScl:F4}@ang={bestAng:F1}°";

                    if (mappedPrevX.HasValue)
                    {
                        coordInfo += $", mappedPrev≈({mappedPrevX.Value:F4},{mappedPrevY.Value:F4})";
                    }

                    coordInfo += $" → FinalCenter=({lvlStats.FinalCenterX:F4},{lvlStats.FinalCenterY:F4})";
                    perfStats.CoordinateChainLog.Add(coordInfo);
                }
                else
                {
                    // ★ 坐标监控：输出非最细层坐标链路摘要
                    string refSrc = isFullSearch ? "FullSearch" : "Refine";
                    string coordInfo = $"  [Lv{level}]({refSrc}) " +
                        $"img={levelData.Width}x{levelData.Height}, " +
                        $"Scale=({lvlStats.ScaleX:F6},{lvlStats.ScaleY:F6}), " +
                        $"candidates={candidates.Count}, " +
                        $"best=({bestX},{bestY})@sc={bestScl:F4}@ang={bestAng:F1}°";

                    if (mappedPrevX.HasValue)
                    {
                        coordInfo += $", mappedPrev≈({mappedPrevX.Value:F4},{mappedPrevY.Value:F4})";
                    }

                    perfStats.CoordinateChainLog.Add(coordInfo);
                }

                perfStats.Levels.Add(lvlStats);

                prevCandidates = candidates;
            }

            foreach (var p in pyramid)
            {
                if (p.ResponseMaps != null)
                    foreach (var map in p.ResponseMaps)
                        Array.Clear(map, 0, map.Length);
            }

            matchResults = matchResults.OrderByDescending(r => r.Score).ToList();
            perfStats.TotalMs = overallSw.ElapsedMilliseconds;
        }

        #endregion

        #region 私有辅助 — 形状特征计算

        /// <summary>
        /// 从 CC stats 直接计算矩形度（零 FindContours 开销）
        /// </summary>
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

        #region 私有辅助 — 形状匹配（响应图引擎）

        private const int DIRECTION_BINS = 16;
        private const double BIN_ANGLE_DEG = 360.0 / DIRECTION_BINS; // 22.5°

        /// <summary>
        /// 方向相似度 LUT（byte 0~255，预乘避免 float 运算）
        /// </summary>
        private static readonly byte[,] DIRECTION_LUT = BuildDirectionLut();

        private static byte[,] BuildDirectionLut()
        {
            var lut = new byte[DIRECTION_BINS, DIRECTION_BINS];
            for (int dt = 0; dt < DIRECTION_BINS; dt++)
                for (int di = 0; di < DIRECTION_BINS; di++)
                {
                    double diff = (dt - di) * BIN_ANGLE_DEG * Math.PI / 180.0;
                    double sim = Math.Max(0.0, Math.Cos(diff));
                    lut[dt, di] = (byte)(sim * 255);
                }
            return lut;
        }

        /// <summary>
        /// 形状匹配结果
        /// </summary>
        public class ShapeMatchResult
        {
            public double Score { get; set; }
            /// <summary>匹配点比率 = 有效匹配点数 / 总模板点数 [0, 1]</summary>
            public double MatchRatio { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double Angle { get; set; }
            public double Scale { get; set; }
        }

        /// <summary>
        /// 形状匹配每层性能统计
        /// </summary>
        /// <summary>
        /// 搜索内部统计（用于性能分析，由 FullSearch/RefineSearch 内部填充）
        /// </summary>
        internal struct SearchStats
        {
            public int ScaleCombosTried;            // 实际执行的缩放组合数
            public int AngleCombosTried;            // 实际执行的角度组合数
            public long SpatialPixelsSearched;      // 实际搜索的像素位置数
            public long TotalTemplateChecks;        // 累计模板点检查次数（含提前终止截断）
            public long EarlyTerminations;          // 提前终止触发次数
            public long EarlyTerminationChecks;     // 提前终止时累计检查点数（用于计算平均值）
            public int CandidatesOut;               // 输出候选数

            public readonly double EarlyTerminationAvgPoint =>
                EarlyTerminations > 0 ? (double)EarlyTerminationChecks / EarlyTerminations : 0;
        }

        public class MatchPerformanceStats
        {
            public int TotalTemplatePoints { get; set; }
            public int PyramidLevelCount { get; set; }
            public long BuildPyramidMs { get; set; }

            public class LevelStats
            {
                public int Level { get; set; }
                public int ImageW { get; set; }
                public int ImageH { get; set; }
                public double ScaleX { get; set; }  // 该层相对原图的 X 缩放比
                public double ScaleY { get; set; }  // 该层相对原图的 Y 缩放比
                public int TemplatePointsUsed { get; set; }
                public long SearchMs { get; set; }
                public int Candidates { get; set; }
                public bool IsFullSearch { get; set; }

                // ★ 搜索过程性能明细
                public int AngleCombosTried { get; set; }      // 角度组合数
                public int ScaleCombosTried { get; set; }      // 缩放组合数
                public long SpatialPixelsSearched { get; set; } // 搜索像素位置数
                public long TotalTemplateChecks { get; set; }  // 累计模板点检查次数
                public long EarlyTerminations { get; set; }     // 提前终止次数
                public double EarlyTerminationAvgPoint { get; set; } // 提前终止时平均检查点数

                // ★ 坐标监控：该层最优候选
                public double BestScore { get; set; }
                public int BestX { get; set; }       // 在该层图像空间的整数坐标
                public int BestY { get; set; }
                public double BestAngle { get; set; }
                public double BestScale { get; set; }
                public double? MappedFromPrevX { get; set; } // Refine 时：粗层候选映射到本层的浮点坐标（Round 前）
                public double? MappedFromPrevY { get; set; }

                // ★ 最终映射到原图的坐标（仅 finest level 有效）
                public double? FinalCenterX { get; set; }
                public double? FinalCenterY { get; set; }
            }

            public List<LevelStats> Levels { get; set; } = new();
            public long TotalMs { get; set; }

            /// <summary>
            /// 坐标链路诊断消息（由 FindShape 内部追加，调用方统一输出）
            /// </summary>
            public List<string> CoordinateChainLog { get; set; } = new();
        }

        /// <summary>
        /// 响应图金字塔层级数据
        /// </summary>
        private struct ResponsePyramidLevel
        {
            public ushort[][] ResponseMaps { get; set; } // 8 x [height * width]
            public int Width { get; set; }
            public int Height { get; set; }
            public double ScaleX { get; set; }  // 相对 Level 0 的实际 X 缩放比 = origW / Width
            public double ScaleY { get; set; }  // 相对 Level 0 的实际 Y 缩放比 = origH / Height
        }


        /// <summary>
        /// 从 EdgePoint 列表量化方向到 byte 0~(DIRECTION_BINS-1)
        /// </summary>
        private static byte[] QuantizeEdgePointsDirections(List<EdgePoint> edgePoints)
        {
            byte[] quantized = new byte[edgePoints.Count];
            double binRad = 2.0 * Math.PI / DIRECTION_BINS;
            for (int i = 0; i < edgePoints.Count; i++)
            {
                double angle = edgePoints[i].Direction;
                if (angle < 0) angle += 2 * Math.PI;
                quantized[i] = (byte)(angle / binRad % DIRECTION_BINS);
            }
            return quantized;
        }

        /// <summary>
        /// 预计算方向量化表（基于量化后的 gx/gy 查表替代 atan2，加速 ~40×）
        /// Scharr 输出为整数 [-4080, 4080]，量化到 [-255, 255]（除以 16），表大小 511×511
        /// </summary>
        private static readonly byte[,] QUANTIZED_DIR_LUT = BuildQuantizedDirLut();

        private const int DIR_LUT_SHIFT = 16;   // 量化步长：大值不影响方向精度，小梯度方向噪声不影响分数
        private const int DIR_LUT_OFFSET = 255;
        private const int DIR_LUT_SIZE = 511;   // [-255, +255]

        private static byte[,] BuildQuantizedDirLut()
        {
            var lut = new byte[DIR_LUT_SIZE, DIR_LUT_SIZE];
            double binRad = 2.0 * Math.PI / DIRECTION_BINS;

            for (int i = -255; i <= 255; i++)
            {
                for (int j = -255; j <= 255; j++)
                {
                    // 量化后的近似梯度值
                    float gxVal = i * DIR_LUT_SHIFT;
                    float gyVal = j * DIR_LUT_SHIFT;
                    double angle = Math.Atan2(gyVal, gxVal);
                    if (angle < 0) angle += 2 * Math.PI;
                    lut[i + DIR_LUT_OFFSET, j + DIR_LUT_OFFSET] =
                        (byte)(angle / binRad % DIRECTION_BINS);
                }
            }
            return lut;
        }

        /// <summary>
        /// 根据 Gx/Gy 量化方向到 0~(DIRECTION_BINS-1) — 查表版（替代 atan2）
        /// </summary>
        private static int QuantizeDirectionTable(float gx, float gy)
        {
            int idx = ((int)gx / DIR_LUT_SHIFT) + DIR_LUT_OFFSET;
            int idy = ((int)gy / DIR_LUT_SHIFT) + DIR_LUT_OFFSET;
            if ((uint)idx >= DIR_LUT_SIZE) idx = idx < 0 ? 0 : DIR_LUT_SIZE - 1;
            if ((uint)idy >= DIR_LUT_SIZE) idy = idy < 0 ? 0 : DIR_LUT_SIZE - 1;
            return QUANTIZED_DIR_LUT[idx, idy];
        }

        /// <summary>
        /// 根据 Gx/Gy 量化方向到 0~(DIRECTION_BINS-1)（使用 atan2，保留以作对比参考）
        /// </summary>
        private static int QuantizeDirectionAtan2(float gx, float gy)
        {
            double angle = Math.Atan2(gy, gx); // [-π, π]
            if (angle < 0) angle += 2 * Math.PI;
            double binRad = 2.0 * Math.PI / DIRECTION_BINS;
            return (int)(angle / binRad) % DIRECTION_BINS;
        }

        /// <summary>
        /// 构建响应图金字塔（响应图 = 预乘 LUT 的 ushort 梯度幅值图 x 8）
        /// </summary>
        private static List<ResponsePyramidLevel> BuildResponsePyramid(Mat grayImage, int levels, int minLevel = 0)
        {
            var pyramid = new List<ResponsePyramidLevel>(levels);
            using var current = grayImage.Clone();
            int origW = grayImage.Width;
            int origH = grayImage.Height;

            for (int i = 0; i < levels; i++)
            {
                int curW = current.Width;
                int curH = current.Height;

                if (i >= minLevel)
                {
                    using var gx = new Mat();
                    using var gy = new Mat();
                    Cv2.Scharr(current, gx, MatType.CV_32F, 1, 0);
                    Cv2.Scharr(current, gy, MatType.CV_32F, 0, 1);

                    int w = gx.Width;
                    int h = gy.Height;
                    int total = w * h;

                    var maps = new ushort[DIRECTION_BINS][];
                    for (int b = 0; b < DIRECTION_BINS; b++)
                        maps[b] = new ushort[total];

                    const byte INVALID_DIR = 255;
                    unsafe
                    {
                        float* gxPtr = (float*)gx.Data.ToPointer();
                        float* gyPtr = (float*)gy.Data.ToPointer();

                        System.Threading.Tasks.Parallel.ForEach(
                            System.Collections.Concurrent.Partitioner.Create(0, h),
                            range =>
                        {
                            for (int y = range.Item1; y < range.Item2; y++)
                            {
                                for (int x = 0; x < w; x++)
                                {
                                    int idx = y * w + x;
                                    float gxVal = gxPtr[idx];
                                    float gyVal = gyPtr[idx];
                                    float mag = (float)Math.Sqrt(gxVal * gxVal + gyVal * gyVal);

                                    // 低梯度像素标记为无效方向，maps[dt][idx] 保持 0
                                    byte qdir = mag >= 1.0f
                                        ? (byte)QuantizeDirectionTable(gxVal, gyVal)
                                        : INVALID_DIR;

                                    // ★ 仅存储方向相似度(0~255)，不含梯度幅值
                                    //   标准做法: score = (1/N) × Σ cos(θt - θi)，与对比度无关
                                    if (qdir != INVALID_DIR)
                                    {
                                        for (int dt = 0; dt < DIRECTION_BINS; dt++)
                                            maps[dt][idx] = DIRECTION_LUT[dt, qdir];
                                    }
                                }
                            }
                        });
                    }

                    pyramid.Add(new ResponsePyramidLevel
                    {
                        ResponseMaps = maps,
                        Width = w,
                        Height = h,
                        ScaleX = (double)origW / w,
                        ScaleY = (double)origH / h
                    });
                }
                else
                {
                    // 跳过响应图建造（仅用于 PyrDown 传播到下一层），仍记录实际尺寸和缩放比
                    pyramid.Add(new ResponsePyramidLevel
                    {
                        ResponseMaps = null,
                        Width = curW,
                        Height = curH,
                        ScaleX = (double)origW / curW,
                        ScaleY = (double)origH / curH
                    });
                }

                if (i < levels - 1)
                {
                    using var pyrDown = new Mat();
                    Cv2.PyrDown(current, pyrDown, new Size(current.Width / 2, current.Height / 2));
                    pyrDown.CopyTo(current);
                }
            }
            return pyramid;
        }

        /// <summary>
        /// 全图搜索最粗层 — 纯 ushort 查表匹配
        /// </summary>
        private static void FullSearchOnResponseMaps(
            ushort[][] responseMaps, int imgW, int imgH,
            List<Point2f> normalizedContour, byte[] quantizedTemplateDirs,
            double minScore,
            double angleStart, double angleEnd, double angleStep,
            double scaleMin, double scaleMax, double scaleStep,
            int stepSize,
            List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)> candidates,
            Stopwatch stopwatch, int timeoutMs,
            ref SearchStats stats)
        {
            int tmplCount = normalizedContour.Count;
            if (tmplCount < 3) return;

            double normalizer = 1.0 / (tmplCount * 255.0);
            int angleSteps = Math.Max(1, (int)Math.Ceiling((angleEnd - angleStart) / angleStep));
            int scaleSteps = Math.Max(1, (int)Math.Ceiling((scaleMax - scaleMin) / scaleStep));

            long localChecks = 0;
            long localEarlyTerm = 0;
            long localEarlyChecks = 0;
            long localSpatialPixels = 0;

            for (int si = 0; si < scaleSteps; si++)
            {
                if (stopwatch.ElapsedMilliseconds > timeoutMs) break;
                stats.ScaleCombosTried++;

                double scale = Math.Clamp(scaleMin + si * scaleStep, scaleMin, scaleMax);

                int[] sxPos = new int[tmplCount];
                int[] syPos = new int[tmplCount];
                for (int ti = 0; ti < tmplCount; ti++)
                {
                    sxPos[ti] = (int)Math.Round(normalizedContour[ti].X * scale);
                    syPos[ti] = (int)Math.Round(normalizedContour[ti].Y * scale);
                }

                for (int ai = 0; ai < angleSteps; ai++)
                {
                    if (stopwatch.ElapsedMilliseconds > timeoutMs) break;
                    stats.AngleCombosTried++;

                    double angleDeg = angleStart + ai * angleStep;
                    double angleRad = angleDeg * Math.PI / 180.0;
                    double cosA = Math.Cos(angleRad);
                    double sinA = Math.Sin(angleRad);

                    int angleBinShift = (int)Math.Round(angleDeg / BIN_ANGLE_DEG);
                    angleBinShift = ((angleBinShift % DIRECTION_BINS) + DIRECTION_BINS) % DIRECTION_BINS;

                    int[] rx = new int[tmplCount];
                    int[] ry = new int[tmplCount];
                    int minTx = int.MaxValue, maxTx = int.MinValue;
                    int minTy = int.MaxValue, maxTy = int.MinValue;

                    for (int ti = 0; ti < tmplCount; ti++)
                    {
                        int rxi = (int)Math.Round(sxPos[ti] * cosA - syPos[ti] * sinA);
                        int ryi = (int)Math.Round(sxPos[ti] * sinA + syPos[ti] * cosA);
                        rx[ti] = rxi;
                        ry[ti] = ryi;
                        if (rxi < minTx) minTx = rxi;
                        if (rxi > maxTx) maxTx = rxi;
                        if (ryi < minTy) minTy = ryi;
                        if (ryi > maxTy) maxTy = ryi;
                    }

                    int tmplW = maxTx - minTx;
                    int tmplH = maxTy - minTy;
                    if (tmplW <= 0 || tmplH <= 0 || tmplW >= imgW || tmplH >= imgH) continue;

                    int searchSX = Math.Max(0, -minTx);
                    int searchSY = Math.Max(0, -minTy);
                    int searchEX = Math.Min(imgW - 1 - maxTx, imgW - 1);
                    int searchEY = Math.Min(imgH - 1 - maxTy, imgH - 1);

                    int xCount = ((searchEX - searchSX) / stepSize) + 1;
                    int yCount = ((searchEY - searchSY) / stepSize) + 1;
                    int spatialCount = xCount * yCount;
                    localSpatialPixels += spatialCount;

                    var rotatedMap = new int[tmplCount];
                    for (int ti = 0; ti < tmplCount; ti++)
                        rotatedMap[ti] = (quantizedTemplateDirs[ti] + angleBinShift) % DIRECTION_BINS;

                    for (int sy = searchSY; sy <= searchEY; sy += stepSize)
                    {
                        for (int sx = searchSX; sx <= searchEX; sx += stepSize)
                        {
                            int sum = 0;
                            int matchCount = 0;
                            int ti;
                            for (ti = 0; ti < tmplCount; ti++)
                            {
                                localChecks++;

                                int px = sx + rx[ti];
                                int py = sy + ry[ti];
                                if ((uint)px < (uint)imgW && (uint)py < (uint)imgH)
                                {
                                    int val = responseMaps[rotatedMap[ti]][py * imgW + px];
                                    sum += val;
                                    if (val >= 128) matchCount++;
                                }

                                int remaining = tmplCount - ti - 1;
                                double maxPossible = (sum + remaining * 255) * normalizer;
                                if (maxPossible < minScore)
                                {
                                    sum = -1;
                                    localEarlyTerm++;
                                    localEarlyChecks += (ti + 1);
                                    break;
                                }
                            }

                            if (sum >= 0)
                            {
                                double score = sum * normalizer;
                                if (score >= minScore)
                                {
                                    candidates.Add((score, sx, sy, angleDeg, scale, matchCount));
                                    stats.CandidatesOut++;
                                }
                            }
                        }
                    }
                }
            }

            stats.TotalTemplateChecks += localChecks;
            stats.EarlyTerminations += localEarlyTerm;
            stats.EarlyTerminationChecks += localEarlyChecks;
            stats.SpatialPixelsSearched += localSpatialPixels;
        }

        /// <summary>
        /// 邻域精化搜索 — 在前一粗层候选位置 + 角度 + 缩放 三维邻域内查表匹配
        /// </summary>
        private static void RefineSearchOnResponseMaps(
            ushort[][] responseMaps, int imgW, int imgH,
            List<Point2f> normalizedContour, byte[] quantizedTemplateDirs,
            double minScore, int stepSize,
            List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)> candidates,
            List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)> prevCandidates,
            double angleStep, double scaleStep, int prevLevelWidth, int prevLevelHeight,
            Stopwatch stopwatch, int timeoutMs,
            ref SearchStats stats)
        {
            int tmplCount = normalizedContour.Count;
            if (tmplCount < 3) return;

            double normalizer = 1.0 / (tmplCount * 255.0);
            int searchRadius = Math.Max(stepSize * 2, 6);
            const double ANGLE_MARGIN_FACTOR = 1.5;
            const double SCALE_MARGIN_FACTOR = 1.5;

            long localChecks = 0;
            long localEarlyTerm = 0;
            long localEarlyChecks = 0;
            long localSpatialPixels = 0;

            foreach (var prev in prevCandidates)
            {
                if (stopwatch.ElapsedMilliseconds > timeoutMs) break;

                double refAngle = prev.Angle;
                double refScale = prev.Scale;

                // 角度邻域: [refAngle - margin, refAngle + margin]
                double aStart = refAngle - angleStep * ANGLE_MARGIN_FACTOR;
                double aEnd = refAngle + angleStep * ANGLE_MARGIN_FACTOR;
                int aSteps = Math.Max(1, (int)((aEnd - aStart) / angleStep) + 1);

                // 缩放邻域: [refScale - margin, refScale + margin]
                double sStart = refScale - scaleStep * SCALE_MARGIN_FACTOR;
                double sEnd = refScale + scaleStep * SCALE_MARGIN_FACTOR;
                int sSteps = Math.Max(1, (int)((sEnd - sStart) / scaleStep) + 1);

                for (int si = 0; si < sSteps; si++)
                {
                    double scale = sStart + si * scaleStep;
                    if (scale <= 0) continue;
                    stats.ScaleCombosTried++;

                    for (int ai = 0; ai < aSteps; ai++)
                    {
                        if (stopwatch.ElapsedMilliseconds > timeoutMs) break;
                        stats.AngleCombosTried++;

                        double angleDeg = aStart + ai * angleStep;
                        double angleRad = angleDeg * Math.PI / 180.0;
                        double cosA = Math.Cos(angleRad);
                        double sinA = Math.Sin(angleRad);

                        int angleBinShift = (int)Math.Round(angleDeg / BIN_ANGLE_DEG);
                        angleBinShift = ((angleBinShift % DIRECTION_BINS) + DIRECTION_BINS) % DIRECTION_BINS;

                        int[] rx = new int[tmplCount];
                        int[] ry = new int[tmplCount];
                        for (int ti = 0; ti < tmplCount; ti++)
                        {
                            float sx = (float)(normalizedContour[ti].X * scale);
                            float sy = (float)(normalizedContour[ti].Y * scale);
                            rx[ti] = (int)Math.Round(sx * cosA - sy * sinA);
                            ry[ti] = (int)Math.Round(sx * sinA + sy * cosA);
                        }

                        var rotatedMap = new int[tmplCount];
                        for (int ti = 0; ti < tmplCount; ti++)
                            rotatedMap[ti] = (quantizedTemplateDirs[ti] + angleBinShift) % DIRECTION_BINS;

                        double ratioX = (double)imgW / prevLevelWidth;
                        double ratioY = (double)imgH / prevLevelHeight;
                        int centerX = (int)Math.Round(prev.X * ratioX);
                        int centerY = (int)Math.Round(prev.Y * ratioY);

                        int xStart = Math.Max(0, centerX - searchRadius);
                        int xEnd = Math.Min(imgW - 1, centerX + searchRadius);
                        int yStart = Math.Max(0, centerY - searchRadius);
                        int yEnd = Math.Min(imgH - 1, centerY + searchRadius);

                        int xCount = ((xEnd - xStart) / stepSize) + 1;
                        int yCount = ((yEnd - yStart) / stepSize) + 1;
                        localSpatialPixels += (long)xCount * yCount;

                        for (int sy = yStart; sy <= yEnd; sy += stepSize)
                        {
                            for (int sx = xStart; sx <= xEnd; sx += stepSize)
                            {
                                int sum = 0;
                                int matchCount = 0;
                                int ti;
                                for (ti = 0; ti < tmplCount; ti++)
                                {
                                    localChecks++;

                                    int px = sx + rx[ti];
                                    int py = sy + ry[ti];
                                    if ((uint)px < (uint)imgW && (uint)py < (uint)imgH)
                                    {
                                        int val = responseMaps[rotatedMap[ti]][py * imgW + px];
                                        sum += val;
                                        if (val >= 128) matchCount++;
                                    }

                                    int remaining = tmplCount - ti - 1;
                                    double maxPossible = (sum + remaining * 255) * normalizer;
                                    if (maxPossible < minScore)
                                    {
                                        sum = -1;
                                        localEarlyTerm++;
                                        localEarlyChecks += (ti + 1);
                                        break;
                                    }
                                }

                                if (sum >= 0)
                                {
                                    double score = sum * normalizer;
                                    if (score >= minScore)
                                    {
                                        candidates.Add((score, sx, sy, angleDeg, scale, matchCount));
                                        stats.CandidatesOut++;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            stats.TotalTemplateChecks += localChecks;
            stats.EarlyTerminations += localEarlyTerm;
            stats.EarlyTerminationChecks += localEarlyChecks;
            stats.SpatialPixelsSearched += localSpatialPixels;
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
