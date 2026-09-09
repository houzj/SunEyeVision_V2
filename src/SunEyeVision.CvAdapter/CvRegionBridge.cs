using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// CvEngine 区域桥接（适配层）：把框架 RoI 契约（Roi/RoiShape 几何）与
    /// 位置修正矩阵（AffineMatrix）转换为 CvEngine 的纯算法区域 CvRegion。
    /// 这两类转换依赖 SDK 契约类型 + CvEngine 算法类型，按对称架构归适配层；
    /// CvEngine 作为纯算法库不依赖 SDK，HalAdapter 与本项目互不引用。
    /// </summary>
    public static class CvRegionBridge
    {
        /// <summary>
        /// 构建 CvEngine ROI = 检测区域 - 屏蔽区域（两者均无有效区域时返回 null）。
        /// 订阅/拼装区域经统一出口 <see cref="Roi.ResolveRuntime"/> 解析（context 为上游节点结果）。
        /// </summary>
        public static CvRegion? BuildROI(
            Size imageSize,
            IEnumerable<Roi> inspectionRois,
            IEnumerable<Roi> maskRois,
            IParameterContext? context = null)
        {
            bool hasInspection = inspectionRois.Any();
            bool hasMask = maskRois.Any();

            if (hasInspection && hasMask)
            {
                using var inspRegion = CreateFromRegions(imageSize, inspectionRois, context);
                using var maskRegion = CreateFromRegions(imageSize, maskRois, context);
                CvOp.Difference(inspRegion, maskRegion, out var roiRegion);
                return roiRegion;
            }
            else if (hasInspection)
            {
                return CreateFromRegions(imageSize, inspectionRois, context);
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
        /// 由框架 RoI 集合（Roi）栅格化为 CvEngine 区域掩码。
        /// 每个启用 ROI 先经统一出口 <see cref="Roi.ResolveRuntime"/> 解析实际几何
        /// （本地/整对象订阅/参数拼装三态归一），再栅格化；支持的几何：Circle2d / RotatedRect2d / Polygon2d / System.Windows.Rect。
        /// </summary>
        public static CvRegion CreateFromRegions(
            Size imageSize,
            IEnumerable<Roi> rois,
            IParameterContext? context = null)
        {
            var enabled = rois.Where(r => r.IsEnabled).ToList();
            if (enabled.Count == 0)
                return CvRegion.GenEmptyObj();

            var combinedMask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);

            foreach (var roi in enabled)
            {
                foreach (var shape in roi.ResolveRuntime(context))
                {
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
            }

            return CvRegion.FromMask(combinedMask, ownsData: true);
        }
    }
}
