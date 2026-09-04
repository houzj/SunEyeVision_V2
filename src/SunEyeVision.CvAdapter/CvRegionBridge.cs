using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// CvEngine 区域桥接（适配层）：把框架 RoI 契约（RoiData/ShapeParameters 几何）与
    /// 位置修正矩阵（AffineMatrix）转换为 CvEngine 的纯算法区域 CvRegion。
    /// 这两类转换依赖 SDK 契约类型 + CvEngine 算法类型，按对称架构归适配层；
    /// CvEngine 作为纯算法库不依赖 SDK，HalAdapter 与本项目互不引用。
    /// </summary>
    public static class CvRegionBridge
    {
        /// <summary>
        /// 构建 CvEngine ROI = 检测区域 - 屏蔽区域（两者均无有效区域时返回 null）。
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
                using var inspRegion = CreateFromRegions(imageSize, inspectionRegions);
                using var maskRegion = CreateFromRegions(imageSize, maskRegions);
                CvOp.Difference(inspRegion, maskRegion, out var roiRegion);
                return roiRegion;
            }
            else if (hasInspection)
            {
                return CreateFromRegions(imageSize, inspectionRegions);
            }

            return null;
        }

        /// <summary>
        /// 对 CvEngine ROI 应用位置修正（WarpAffine），返回修正后的区域。
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

        /// <summary>
        /// 由框架 RoI 集合（RoiData）栅格化为 CvEngine 区域掩码；仅处理 IsEnabled 且几何有效的项。
        /// 支持的几何：Circle2d / RotatedRect2d / Polygon2d / System.Windows.Rect。
        /// </summary>
        public static CvRegion CreateFromRegions(Size imageSize, IEnumerable<RoiData> regions)
        {
            var enabled = regions.Where(r => r.IsEnabled).ToList();
            if (enabled.Count == 0)
                return CvRegion.GenEmptyObj();

            var combinedMask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);

            foreach (var region in enabled)
            {
                if (region.Parameters is not ShapeParameters shape || shape.Geometry == null) continue;

                if (shape.Geometry is Circle2d c)
                {
                    Cv2.Circle(combinedMask,
                        (int)Math.Round(c.Center.X), (int)Math.Round(c.Center.Y),
                        (int)Math.Round(c.Radius), Scalar.White, -1);
                }
                else if (shape.Geometry is RotatedRect2d rr)
                {
                    int x = (int)Math.Round(rr.CenterX - rr.Width / 2);
                    int y = (int)Math.Round(rr.CenterY - rr.Height / 2);
                    Cv2.Rectangle(combinedMask, new Rect(x, y,
                        (int)Math.Round(rr.Width), (int)Math.Round(rr.Height)), Scalar.White, -1);
                }
                else if (shape.Geometry is Polygon2d poly && poly.Vertices.Length >= 3)
                {
                    var pts = poly.Vertices.Select(v => new Point((int)v.X, (int)v.Y)).ToArray();
                    Cv2.FillPoly(combinedMask, new Point[][] { pts }, Scalar.White);
                }
                else if (shape.Geometry is System.Windows.Rect rect)
                {
                    Cv2.Rectangle(combinedMask, new Rect(
                        (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
                        (int)Math.Round(rect.Width), (int)Math.Round(rect.Height)), Scalar.White, -1);
                }
            }

            return CvRegion.FromMask(combinedMask, ownsData: true);
        }
    }
}
