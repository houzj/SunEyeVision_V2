using System;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
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

        /// <summary>形态学操作（Mat版）</summary>
        public static void MorphologyEx(
            Mat input, out Mat result,
            MorphTypes op,
            MorphShapes kernelShape,
            int kernelWidth, int kernelHeight,
            int iterations = 1,
            BorderTypes borderType = BorderTypes.Constant,
            Scalar? borderValue = null)
        {
            var kernel = Cv2.GetStructuringElement(kernelShape, new Size(kernelWidth, kernelHeight));
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
                    Cv2.MorphologyEx(input, result, op, kernel, new Point(-1, -1), iterations, borderType,
                        borderValue ?? Scalar.All(255));
            }
            finally
            {
                kernel.Dispose();
            }
        }

        /// <summary>形态学操作（CvImage版）</summary>
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
    }
}
