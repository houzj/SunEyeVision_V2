using System;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
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
    }
}
