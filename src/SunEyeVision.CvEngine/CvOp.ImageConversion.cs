using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;

namespace SunEyeVision.CvEngine
{
    public static partial class CvOp
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
                Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
            else
                input.ConvertTo(gray, MatType.CV_8UC1);
            grayImage = new CvImage(gray, ownsImage: true);
        }

        /// <summary>
        /// RGB转灰度（图像数组）- 对应 Halcon rgb1_to_gray(Image(-array), GrayImage(-array))
        /// </summary>
        public static void Rgb1ToGray(CvImage input, out CvImage grayImage)
        {
            if (input.Empty)
            {
                grayImage = CvImage.GenEmptyObj();
                return;
            }

            if (input.Count == 1)
            {
                Rgb1ToGray(input.Image, out grayImage);
                return;
            }

            var items = new List<ImageItem>(input.Count);
            foreach (var item in input._items)
            {
                var src = item.Image;
                if (src.Channels() == 1 && src.Depth() == MatType.CV_8U)
                    items.Add(new ImageItem(src.Clone(), ownsImage: true));
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
        /// </summary>
        public static void ReduceDomain(CvImage image, CvRegion? region, out CvImage imageReduced)
        {
            if (region == null || region.IsEmpty)
            {
                var items = new List<ImageItem>(image.Count);
                foreach (var item in image._items)
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
                imageReduced = items.Count > 0 ? new CvImage(items) : CvImage.GenEmptyObj();
                return;
            }

            var mask = region.Mask;

            if (image.Count == 1)
            {
                var masked = new Mat();
                image.Image.CopyTo(masked, mask);
                imageReduced = new CvImage(masked, ownsImage: true);
                return;
            }

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
        /// </summary>
        public static void CropDomain(CvImage image, CvRegion? region, out CvImage imageCropped, out Rect roiBBox)
        {
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

            using var croppedMask = new Mat(mask, bbox);

            if (image.Count == 1)
            {
                using var croppedImage = new Mat(image.Image, bbox);
                var masked = new Mat();
                croppedImage.CopyTo(masked, croppedMask);
                imageCropped = new CvImage(masked, ownsImage: true);
                roiBBox = bbox;
                return;
            }

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
    }
}
