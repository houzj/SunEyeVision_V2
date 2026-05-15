using System;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    // 注意：此为 CvOp 的 partial 定义文件之一 — 显示与掩码转换
    public static partial class CvOp
    {
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
    }
}
