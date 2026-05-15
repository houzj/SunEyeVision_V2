using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
        #region 🆕 形状匹配 - 梯度引擎

        /// <summary>形状匹配边缘点 — 对齐 Halcon ModelID 内部结构</summary>
        public class EdgePoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Direction { get; set; }  // 弧度 [-π, π], atan2(Gy, Gx)
            public double Magnitude { get; set; }  // 梯度幅值 sqrt(Gx²+Gy²)
        }

        /// <summary>形状匹配专用梯度提取 — Scharr 3×3 固定算子</summary>
        public static void ComputeShapeGradient(Mat grayImage, out Mat gx, out Mat gy, out Mat mag)
        {
            gx = new Mat();
            gy = new Mat();
            Cv2.Scharr(grayImage, gx, MatType.CV_32F, 1, 0);
            Cv2.Scharr(grayImage, gy, MatType.CV_32F, 0, 1);
            mag = new Mat();
            Cv2.Magnitude(gx, gy, mag);

            Cv2.MinMaxLoc(mag, out _, out double maxVal);
            if (maxVal > 1e-6f)
            {
                double scale = 255.0 / maxVal;
                mag.ConvertTo(mag, MatType.CV_32F, scale);
            }
        }

        /// <summary>
        /// 非极大值抑制 — 沿梯度方向保留局部极大值，细线化到 1 像素
        /// Halcon 8 扇区对齐 + 亚像素线性插值，避免整数硬比较导致的宽缓边缘断裂
        ///
        /// 扇区定义（梯度方向）:
        ///   扇区 0 / 4:  [-22.5°,  22.5°]  / [157.5°, 202.5°]  → 水平 →  ↔ （左右）
        ///   扇区 1 / 5:  [ 22.5°,  67.5°]  / [202.5°, 247.5°]  → 45°  →  ╲ ╱ （左上↔右下）
        ///   扇区 2 / 6:  [ 67.5°, 112.5°]  / [247.5°, 292.5°]  → 垂直 →  ↕ （上下）
        ///   扇区 3 / 7:  [112.5°, 157.5°]  / [292.5°, 337.5°]  → 135° →  ╱ ╲ （左下↔右上）
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
                    double absGx = Math.Abs(gxVal);
                    double absGy = Math.Abs(gyVal);

                    // 将 atan2 角度 [-π, π] 映射到 8 扇区，边界偏移 π/8 (22.5°)
                    double angle = Math.Atan2(gyVal, gxVal) + Math.PI / 8;
                    if (angle < 0) angle += 2 * Math.PI;
                    if (angle >= 2 * Math.PI) angle -= 2 * Math.PI;
                    int sector = (int)(angle / (Math.PI / 4)); // 0-7

                    double interp1, interp2;

                    switch (sector)
                    {
                        // 扇区 0/4: [-22.5°, 22.5°] / [157.5°, 202.5°] → 梯度近水平
                        case 0:
                        case 4:
                        {
                            double weight = absGy / Math.Max(absGx, 1e-10);
                            int sx = gxVal >= 0 ? 1 : -1;
                            int syOff = gyVal >= 0 ? 1 : -1;

                            // 射线先穿过垂直边界 (x±1)，在 y 方向插值
                            interp1 = (1.0 - weight) * magData[y * w + (x + sx)]
                                    + weight * magData[(y + syOff) * w + (x + sx)];
                            interp2 = (1.0 - weight) * magData[y * w + (x - sx)]
                                    + weight * magData[(y - syOff) * w + (x - sx)];
                            break;
                        }

                        // 扇区 2/6: [67.5°, 112.5°] / [247.5°, 292.5°] → 梯度近垂直
                        case 2:
                        case 6:
                        {
                            double weight = absGx / Math.Max(absGy, 1e-10);
                            int sy = gyVal >= 0 ? 1 : -1;
                            int sxOff = gxVal >= 0 ? 1 : -1;

                            // 射线先穿过水平边界 (y±1)，在 x 方向插值
                            interp1 = (1.0 - weight) * magData[(y + sy) * w + x]
                                    + weight * magData[(y + sy) * w + (x + sxOff)];
                            interp2 = (1.0 - weight) * magData[(y - sy) * w + x]
                                    + weight * magData[(y - sy) * w + (x - sxOff)];
                            break;
                        }

                        // 扇区 1/5: [22.5°, 67.5°] / [202.5°, 247.5°] → 梯度 ~45° 对角
                        // 扇区 3/7: [112.5°, 157.5°] / [292.5°, 337.5°] → 梯度 ~135° 对角
                        // 对角扇区根据 |gx|/|gy| 自动选择 E-W 或 N-S 路径（45° 处连续切换）
                        default:
                        {
                            if (absGx >= absGy)
                            {
                                double weight = absGy / Math.Max(absGx, 1e-10);
                                int sx = gxVal >= 0 ? 1 : -1;
                                int syOff = gyVal >= 0 ? 1 : -1;

                                interp1 = (1.0 - weight) * magData[y * w + (x + sx)]
                                        + weight * magData[(y + syOff) * w + (x + sx)];
                                interp2 = (1.0 - weight) * magData[y * w + (x - sx)]
                                        + weight * magData[(y - syOff) * w + (x - sx)];
                            }
                            else
                            {
                                double weight = absGx / Math.Max(absGy, 1e-10);
                                int sy = gyVal >= 0 ? 1 : -1;
                                int sxOff = gxVal >= 0 ? 1 : -1;

                                interp1 = (1.0 - weight) * magData[(y + sy) * w + x]
                                        + weight * magData[(y + sy) * w + (x + sxOff)];
                                interp2 = (1.0 - weight) * magData[(y - sy) * w + x]
                                        + weight * magData[(y - sy) * w + (x - sxOff)];
                            }
                            break;
                        }
                    }

                    if (m >= interp1 && m >= interp2)
                        nmsData[idx] = m;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(nmsData, 0, nms.Data, nmsData.Length);
            return nms;
        }

        /// <summary>从 NMS 结果中收集所有边缘点</summary>
        public static List<EdgePoint> CollectEdgePoints(Mat nms, Mat gx, Mat gy,
            bool autoMinChainLength, int manualMinChainLength, out int actualChainLength,
            int closingRadius = 0)
        {
            actualChainLength = 0;
            int total = (int)nms.Total();
            int w = nms.Width;

            using var binaryNms = new Mat();
            Cv2.Threshold(nms, binaryNms, 0.001, 255, ThresholdTypes.Binary);
            binaryNms.ConvertTo(binaryNms, MatType.CV_8U);

            if (closingRadius > 0)
            {
                using var kernel = Cv2.GetStructuringElement(
                    MorphShapes.Ellipse, new Size(2 * closingRadius + 1, 2 * closingRadius + 1));
                Cv2.MorphologyEx(binaryNms, binaryNms, MorphTypes.Close, kernel);
            }

            Cv2.FindContours(binaryNms, out Point[][] contours, out _,
                RetrievalModes.List, ContourApproximationModes.ApproxNone);

            if (contours.Length == 0) return new List<EdgePoint>();

            int[] chainLengths = contours.Select(c => c.Length).ToArray();
            int threshold = autoMinChainLength
                ? ComputeCliffThreshold(chainLengths)
                : manualMinChainLength;
            actualChainLength = threshold;

            float[] nmsData = new float[total];
            float[] gxData = new float[total];
            float[] gyData = new float[total];
            System.Runtime.InteropServices.Marshal.Copy(nms.Data, nmsData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gx.Data, gxData, 0, total);
            System.Runtime.InteropServices.Marshal.Copy(gy.Data, gyData, 0, total);

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

        /// <summary>空间均匀散布选取特征点</summary>
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

        /// <summary>取唯一链长升序排序后 20% 分位值作为链长阈值</summary>
        public static int ComputePlatformThreshold(int[] chainLengths)
        {
            return ComputeCliffThreshold(chainLengths);
        }

        private static int ComputeCliffThreshold(int[] chainLengths)
        {
            var unique = chainLengths.Distinct().OrderBy(v => v).ToList();
            if (unique.Count <= 1) return unique[0];
            int index = (int)(unique.Count * 0.2);
            return unique[index];
        }

        /// <summary>
        /// 基于最粗层最小边缘点数和最小尺寸自动计算金字塔层数
        /// 点数上限：Lk ≈ L1 / 2^(k-1) ≥ minEdgePointsAtCoarsest
        /// 尺寸上限：minDimension ≥ minImageDimension（16px）
        /// 硬上限：maxLevel=6，保证最粗层最短边≥32px，形状轮廓可辨识
        /// 取三者最小值，范围 [2, maxLevel]
        /// 结束层固定保留后 3 层搜索
        /// </summary>
        public static void AutoComputePyramidLevels(int width, int height, int edgePointCount,
            out int start, out int end)
        {
            const int minImageDimension = 16;
            const int minEdgePointsAtCoarsest = 150;
            const int maxLevel = 6;

            // 1. 图像尺寸上限
            int minDim = Math.Min(width, height);
            int maxByImage = (int)Math.Floor(Math.Log(minDim / (double)minImageDimension, 2)) + 1;
            maxByImage = Math.Clamp(maxByImage, 2, 10);

            // 2. 点数上限：最粗层保留至少 minEdgePointsAtCoarsest 个点
            int maxByEdges = edgePointCount <= minEdgePointsAtCoarsest
                ? 2
                : (int)Math.Floor(Math.Log(edgePointCount / (double)minEdgePointsAtCoarsest, 2)) + 1;
            maxByEdges = Math.Clamp(maxByEdges, 2, 10);

            start = Math.Min(maxByImage, maxByEdges);
            start = Math.Min(start, maxLevel);  // 硬上限 L6：最短边 ≥ 32px
            end = start >= 4 ? start - 3 : 1;
        }

        /// <summary>从梯度幅值图自动计算对比度阈值（Triangle Threshold 法）</summary>
        public static float AutoComputeContrast(Mat mag, Mat? mask = null)
        {
            int total = (int)mag.Total();
            if (total == 0) return 30f;

            float[] data = new float[total];
            System.Runtime.InteropServices.Marshal.Copy(mag.Data, data, 0, total);

            // 1. 统计全量直方图 hist[0..255]
            int[] hist = new int[256];
            int w = mag.Width;
            for (int y = 0; y < mag.Height; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask != null && mask.At<byte>(y, x) == 0) continue;

                    int i = y * w + x;
                    int bin = (int)Math.Round(data[i], MidpointRounding.AwayFromZero);
                    bin = Math.Max(0, Math.Min(255, bin));
                    hist[bin]++;
                }
            }

            // 2. Triangle Threshold 法：找到峰值位置和最后一个非空 bin
            int peakBin = 0;
            int peakCount = 0;
            int lastBin = 0;
            for (int i = 0; i < 256; i++)
            {
                if (hist[i] > peakCount)
                {
                    peakCount = hist[i];
                    peakBin = i;
                }
                if (hist[i] > 0)
                    lastBin = i;
            }

            if (lastBin <= peakBin) return 3f;

            // 3. 从 (peakBin, peakCount) 到 (lastBin, 0) 画基线
            //    遍历 peakBin+1 ~ lastBin-1，找点到基线的最大垂直距离
            double dMax = -1;
            int threshold = peakBin;

            int x1 = peakBin, y1 = peakCount;
            int x2 = lastBin, y2 = 0;
            double dx = x2 - x1;
            double dy = y2 - y1;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len < 1e-10) return 3f;

            for (int i = peakBin + 1; i < lastBin; i++)
            {
                // 点到直线的垂直距离 = |dy*x - dx*y + x2*y1 - y2*x1| / len
                double dist = Math.Abs(dy * i - dx * hist[i] + x2 * y1 - y2 * x1) / len;
                if (dist > dMax)
                {
                    dMax = dist;
                    threshold = i;
                }
            }

            return Math.Max(5f, Math.Min(250f, (float)threshold));
        }

        #endregion

        #region 🔍 形状匹配 - 响应图引擎

        private const int DIRECTION_BINS = 8;
        private const double BIN_ANGLE_DEG = 360.0 / DIRECTION_BINS;

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

        private static readonly byte[,] QUANTIZED_DIR_LUT = BuildQuantizedDirLut();
        private const int DIR_LUT_SHIFT = 16;
        private const int DIR_LUT_OFFSET = 255;
        private const int DIR_LUT_SIZE = 511;

        private static byte[,] BuildQuantizedDirLut()
        {
            var lut = new byte[DIR_LUT_SIZE, DIR_LUT_SIZE];
            double binRad = 2.0 * Math.PI / DIRECTION_BINS;

            for (int i = -255; i <= 255; i++)
                for (int j = -255; j <= 255; j++)
                {
                    float gxVal = i * DIR_LUT_SHIFT;
                    float gyVal = j * DIR_LUT_SHIFT;
                    double angle = Math.Atan2(gyVal, gxVal);
                    if (angle < 0) angle += 2 * Math.PI;
                    lut[i + DIR_LUT_OFFSET, j + DIR_LUT_OFFSET] =
                        (byte)(angle / binRad % DIRECTION_BINS);
                }
            return lut;
        }

        private static int QuantizeDirectionTable(float gx, float gy)
        {
            int idx = ((int)gx / DIR_LUT_SHIFT) + DIR_LUT_OFFSET;
            int idy = ((int)gy / DIR_LUT_SHIFT) + DIR_LUT_OFFSET;
            if ((uint)idx >= DIR_LUT_SIZE) idx = idx < 0 ? 0 : DIR_LUT_SIZE - 1;
            if ((uint)idy >= DIR_LUT_SIZE) idy = idy < 0 ? 0 : DIR_LUT_SIZE - 1;
            return QUANTIZED_DIR_LUT[idx, idy];
        }

        /// <summary>形状匹配结果</summary>
        public class ShapeMatchResult
        {
            public double Score { get; set; }
            public double MatchRatio { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double Angle { get; set; }
            public double Scale { get; set; }
        }

        internal struct SearchStats
        {
            public int ScaleCombosTried;
            public int AngleCombosTried;
            public long SpatialPixelsSearched;
            public long TotalTemplateChecks;
            public long EarlyTerminations;
            public long EarlyTerminationChecks;
            public int CandidatesOut;
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
                public double ScaleX { get; set; }
                public double ScaleY { get; set; }
                public int TemplatePointsUsed { get; set; }
                public long SearchMs { get; set; }
                public long PointPrepMs { get; set; }
                public long FullSearchFirstMs { get; set; }
                public long FullSearchFallbackMs { get; set; }
                public long RefineSearchMs { get; set; }
                public int Candidates { get; set; }
                public bool IsFullSearch { get; set; }
                public int AngleCombosTried { get; set; }
                public int ScaleCombosTried { get; set; }
                public long SpatialPixelsSearched { get; set; }
                public long TotalTemplateChecks { get; set; }
                public long EarlyTerminations { get; set; }
                public double EarlyTerminationAvgPoint { get; set; }
                public double BestScore { get; set; }
                public int BestX { get; set; }
                public int BestY { get; set; }
                public double BestAngle { get; set; }
                public double BestScale { get; set; }
                public double? MappedFromPrevX { get; set; }
                public double? MappedFromPrevY { get; set; }
                public double? FinalCenterX { get; set; }
                public double? FinalCenterY { get; set; }
            }

            public List<LevelStats> Levels { get; set; } = new();
            public long TotalMs { get; set; }
            public List<string> CoordinateChainLog { get; set; } = new();
        }

        private struct ResponsePyramidLevel
        {
            public ushort[][] ResponseMaps { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public double ScaleX { get; set; }
            public double ScaleY { get; set; }
        }

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
        /// 形状匹配 FindShape — 使用每层独立边缘点金字塔
        /// </summary>
        public static void FindShape(
            Mat grayImage,
            Dictionary<int, List<EdgePoint>> pyramidEdgePoints,
            double minScore,
            int numMatches,
            double maxOverlap,
            double greediness,
            double angleStart, double angleEnd, double angleStep,
            double scaleMin, double scaleMax, double scaleStep,
            int pyramidStart, int pyramidEnd, int timeoutMs,
            MinContrastMode minContrastMode, float minContrast,
            out List<ShapeMatchResult> matchResults,
            out MatchPerformanceStats perfStats)
        {
            matchResults = new List<ShapeMatchResult>();
            perfStats = new MatchPerformanceStats
            {
                TotalTemplatePoints = pyramidEdgePoints?.GetValueOrDefault(1)?.Count ?? 0
            };

            if (grayImage == null || grayImage.Empty()) return;
            if (pyramidEdgePoints == null || pyramidEdgePoints.Count == 0) return;

            var overallSw = Stopwatch.StartNew();

            int buildLevels = pyramidStart;


            var buildSw = Stopwatch.StartNew();
            var pyramid = BuildResponsePyramid(grayImage, buildLevels, pyramidEnd - 1, minContrastMode, minContrast);
            buildSw.Stop();
            perfStats.BuildPyramidMs = buildSw.ElapsedMilliseconds;
            perfStats.PyramidLevelCount = pyramid.Count;

            foreach (var lvlData in pyramid)
            {
                int lvlIdx = pyramid.IndexOf(lvlData);
                int lvlNum = lvlIdx + 1;
                if (lvlData.ResponseMaps != null)
                {
                    perfStats.CoordinateChainLog.Add(
                        $"  [Lv{lvlNum}] 响应图尺寸={lvlData.Width}x{lvlData.Height}, " +
                        $"ScaleX={lvlData.ScaleX:F6}, ScaleY={lvlData.ScaleY:F6}");
                }
                else
                {
                    perfStats.CoordinateChainLog.Add(
                        $"  [Lv{lvlNum}](跳过) 尺寸={lvlData.Width}x{lvlData.Height}, " +
                        $"ScaleX={lvlData.ScaleX:F6}, ScaleY={lvlData.ScaleY:F6}");
                }
            }

            int actualMaxLevel = Math.Min(pyramidStart, pyramid.Count);

            List<(double Score, int X, int Y, double Angle, double Scale, int MatchedCount)>? prevCandidates = null;

            for (int level = actualMaxLevel; level >= pyramidEnd; level--)
            {
                if (overallSw.ElapsedMilliseconds > timeoutMs) break;

                var levelData = pyramid[level - 1];
                if (levelData.ResponseMaps == null) continue;

                bool isCoarsest = (level == actualMaxLevel);
                bool isFinest = (level == pyramidEnd);

                if (!pyramidEdgePoints.TryGetValue(level, out var levelPoints))
                    throw new InvalidOperationException(
                        $"FindShape: 金字塔 Level {level} 边缘点数据缺失" +
                        $" (范围: {pyramidEnd}~{pyramidStart})");

                if (levelPoints.Count < 3)
                {
                    prevCandidates = null;
                    continue;
                }

                // 数据已在创建时做好稀疏化，匹配时直接使用
                byte[] quantizedDirs = QuantizeEdgePointsDirections(levelPoints);
                int tmplCount = levelPoints.Count;

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
                        searchStats.ScaleCombosTried += fallbackStats.ScaleCombosTried;
                        searchStats.AngleCombosTried += fallbackStats.AngleCombosTried;
                        searchStats.SpatialPixelsSearched += fallbackStats.SpatialPixelsSearched;
                        searchStats.TotalTemplateChecks += fallbackStats.TotalTemplateChecks;
                        searchStats.EarlyTerminations += fallbackStats.EarlyTerminations;
                        searchStats.EarlyTerminationChecks += fallbackStats.EarlyTerminationChecks;
                        searchStats.CandidatesOut += fallbackStats.CandidatesOut;
                    }

                    if (candidates.Count == 0 && level > pyramidEnd)
                        break;
                }
                else
                {
                    double levelMinScore = isFinest ? minScore : minScore * 0.7;

                    var prevLevelData = pyramid[level];
                    RefineSearchOnResponseMaps(levelData.ResponseMaps, levelData.Width, levelData.Height,
                        normalizedContour, quantizedDirs,
                        levelMinScore, 2, candidates, prevCandidates,
                        angleStep, scaleStep, prevLevelData.Width, prevLevelData.Height, overallSw, timeoutMs,
                        ref searchStats);
                }

                searchSw.Stop();

                if (level > pyramidEnd && candidates.Count > 100)
                    candidates = candidates.OrderByDescending(c => c.Score).Take(100).ToList();

                (double bestScore, int bestX, int bestY, double bestAng, double bestScl, _) =
                    candidates.Count > 0
                        ? candidates.OrderByDescending(c => c.Score).First()
                        : (0.0, 0, 0, 0.0, 0.0, 0);

                double? mappedPrevX = null, mappedPrevY = null;
                if (!isFullSearch && prevCandidates is { Count: > 0 })
                {
                    var prevLevelData = pyramid[level];
                    double rX = (double)levelData.Width / prevLevelData.Width;
                    double rY = (double)levelData.Height / prevLevelData.Height;
                    mappedPrevX = prevCandidates.OrderByDescending(c => c.Score).First().X * rX;
                    mappedPrevY = prevCandidates.OrderByDescending(c => c.Score).First().Y * rY;
                }

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

                    if (candidates.Count > 0)
                    {
                        var best = candidates.OrderByDescending(c => c.Score).First();
                        lvlStats.FinalCenterX = best.X * scaleX;
                        lvlStats.FinalCenterY = best.Y * scaleY;
                    }

                    string refSrc = isFullSearch ? "FullSearch" : "Refine";
                    string coordInfo = $"  [Lv{level}]({refSrc}) " +
                        $"img={levelData.Width}x{levelData.Height}, " +
                        $"Scale=({scaleX:F6},{scaleY:F6}), " +
                        $"candidates={candidates.Count}, " +
                        $"best=({bestX},{bestY})@sc={bestScl:F4}@ang={bestAng:F1}°";
                    if (mappedPrevX.HasValue)
                        coordInfo += $", mappedPrev=({mappedPrevX.Value:F4},{mappedPrevY.Value:F4})";
                    coordInfo += $" -> FinalCenter=({lvlStats.FinalCenterX:F4},{lvlStats.FinalCenterY:F4})";
                    perfStats.CoordinateChainLog.Add(coordInfo);
                }
                else
                {
                    string refSrc = isFullSearch ? "FullSearch" : "Refine";
                    string coordInfo = $"  [Lv{level}]({refSrc}) " +
                        $"img={levelData.Width}x{levelData.Height}, " +
                        $"Scale=({lvlStats.ScaleX:F6},{lvlStats.ScaleY:F6}), " +
                        $"candidates={candidates.Count}, " +
                        $"best=({bestX},{bestY})@sc={bestScl:F4}@ang={bestAng:F1}°";
                    if (mappedPrevX.HasValue)
                        coordInfo += $", mappedPrev=({mappedPrevX.Value:F4},{mappedPrevY.Value:F4})";
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

        /// <summary>构建响应图金字塔</summary>
        private static List<ResponsePyramidLevel> BuildResponsePyramid(Mat grayImage, int levels, int minLevel = 0,
            MinContrastMode minContrastMode = MinContrastMode.Auto, float minContrast = 10f)
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

                    float levelMinContrast = ComputeLevelMinContrast(gx, gy, total, minContrastMode, minContrast);

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

                                    byte qdir = mag >= levelMinContrast
                                        ? (byte)QuantizeDirectionTable(gxVal, gyVal)
                                        : INVALID_DIR;

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

        /// <summary>计算本层响应图的最小梯度阈值（Triangle Threshold 采样版）</summary>
        private static float ComputeLevelMinContrast(Mat gx, Mat gy, int total,
            MinContrastMode mode, float manualValue)
        {
            if (mode != MinContrastMode.Auto)
                return manualValue;

            // Auto 模式：Triangle Threshold 采样版（与模板创建时 AutoComputeContrast 算法一致）
            unsafe
            {
                float* gxPtr = (float*)gx.Data.ToPointer();
                float* gyPtr = (float*)gy.Data.ToPointer();

                int sampleCount = Math.Min(total, 10000);
                int[] hist = new int[256];
                var rand = new Random(42);
                for (int i = 0; i < sampleCount; i++)
                {
                    int idx = rand.Next(total);
                    float magVal = (float)Math.Sqrt(gxPtr[idx] * gxPtr[idx] + gyPtr[idx] * gyPtr[idx]);
                    int bin = (int)Math.Round(magVal, MidpointRounding.AwayFromZero);
                    bin = Math.Max(0, Math.Min(255, bin));
                    hist[bin]++;
                }

                // Triangle Threshold
                int peakBin = 0;
                int peakCount = 0;
                int lastBin = 0;
                for (int i = 0; i < 256; i++)
                {
                    if (hist[i] > peakCount)
                    {
                        peakCount = hist[i];
                        peakBin = i;
                    }
                    if (hist[i] > 0)
                        lastBin = i;
                }

                if (lastBin <= peakBin) return 3f;

                double dMax = -1;
                int threshold = peakBin;

                int x1 = peakBin, y1 = peakCount;
                int x2 = lastBin, y2 = 0;
                double dx = x2 - x1;
                double dy = y2 - y1;
                double len = Math.Sqrt(dx * dx + dy * dy);

                if (len < 1e-10) return 3f;

                for (int i = peakBin + 1; i < lastBin; i++)
                {
                    double dist = Math.Abs(dy * i - dx * hist[i] + x2 * y1 - y2 * x1) / len;
                    if (dist > dMax)
                    {
                        dMax = dist;
                        threshold = i;
                    }
                }

                return Math.Max(5f, Math.Min(250f, (float)threshold));
            }
        }

        /// <summary>全图搜索最粗层</summary>
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

        /// <summary>邻域精化搜索</summary>
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

                double aStart = refAngle - angleStep * ANGLE_MARGIN_FACTOR;
                double aEnd = refAngle + angleStep * ANGLE_MARGIN_FACTOR;
                int aSteps = Math.Max(1, (int)((aEnd - aStart) / angleStep) + 1);

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
    }
}
