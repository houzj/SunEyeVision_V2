using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// CvEngine 区域处理辅助类 - 消除各 CvEngine 工具中重复的 ROI 构建、位置修正等代码
    /// </summary>
    public static class CvRegionHelper
    {
        /// <summary>
        /// 构建 CvEngine ROI = 检测区域 - 屏蔽区域
        /// </summary>
        public static CvRegion? BuildROI(
            Size imageSize,
            IEnumerable<RoiData> inspectionRegions,
            IEnumerable<RoiData> maskRegions)
        {
            bool hasInspection = inspectionRegions.Any();
            bool hasMask = maskRegions.Any();

            if (hasInspection && hasMask)
            {
                using var inspRegion = CvRegion.CreateFromRegions(imageSize, inspectionRegions);
                using var maskRegion = CvRegion.CreateFromRegions(imageSize, maskRegions);
                CvOp.Difference(inspRegion, maskRegion, out var roiRegion);
                return roiRegion;
            }
            else if (hasInspection)
            {
                return CvRegion.CreateFromRegions(imageSize, inspectionRegions);
            }

            return null;
        }

        /// <summary>
        /// 对 CvEngine ROI 应用位置修正（WarpAffine）
        /// </summary>
        public static CvRegion? ApplyPositionCorrection(CvRegion? roiRegion, AffineMatrix matrix, Size imageSize)
        {
            if (roiRegion == null) return null;

            CvOp.RegionToMask(roiRegion, out var maskMat);
            using (maskMat)
            {
                var warpMat = new Mat(2, 3, MatType.CV_64F, Scalar.All(0));
                warpMat.Set<double>(0, 0, matrix.M11);
                warpMat.Set<double>(0, 1, matrix.M12);
                warpMat.Set<double>(0, 2, matrix.OffsetX);
                warpMat.Set<double>(1, 0, matrix.M21);
                warpMat.Set<double>(1, 1, matrix.M22);
                warpMat.Set<double>(1, 2, matrix.OffsetY);
                using (warpMat)
                {
                    var warpedMask = new Mat();
                    Cv2.WarpAffine(maskMat, warpedMask, warpMat, imageSize);
                    roiRegion.Dispose();
                    var result = CvRegion.FromMask(warpedMask);
                    warpedMask.Dispose();
                    return result;
                }
            }
        }
    }
}
