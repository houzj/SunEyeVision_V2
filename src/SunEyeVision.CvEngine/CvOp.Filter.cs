using System;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
    {
        #region 🎛️ 图像滤波

        /// <summary>均值滤波 - 对应 Halcon mean_image</summary>
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

        /// <summary>高斯滤波 - 对应 Halcon gauss_image</summary>
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

        /// <summary>中值滤波 - 对应 Halcon median_image</summary>
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

        /// <summary>双边滤波 - 对应 Halcon bilateral_filter</summary>
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

        /// <summary>自定义卷积滤波 - 对应 Halcon convol_image</summary>
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
    }
}
