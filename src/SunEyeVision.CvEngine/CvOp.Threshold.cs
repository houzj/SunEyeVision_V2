using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
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
                result = CvRegion.FromMask(fullBinary.Clone(), ownsData: true);

            if (invert)
            {
                Complement(result, out var complemented);
                result.Dispose();
                result = complemented;
            }
        }

        /// <summary>
        /// 灰度范围阈值 - 对应 Halcon threshold(Image, Region, MinGray, MaxGray)
        /// </summary>
        public static void ThresholdRange(
            CvImage image, CvRegion? region,
            int minGray, int maxGray,
            out CvRegion regionResult)
        {
            ThresholdRange(image, region, new[] { minGray }, new[] { maxGray }, out regionResult);
        }

        /// <summary>
        /// 多区间灰度范围阈值
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
                    items.Add(new RegionItem(binaryMask));
                else
                    binaryMask.Dispose();
            }

            regionResult = items.Count > 0
                ? new CvRegion(items, imageSize)
                : CvRegion.GenEmptyObj();
        }

        #endregion
    }
}
