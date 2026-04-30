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
