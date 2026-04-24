using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// OpenCV图像封装类 - 对应Halcon的HImage
    /// </summary>
    /// <remarks>
    /// 算子对照表：
    /// | Halcon算子           | 本类方法           | 说明                     |
    /// |----------------------|--------------------|--------------------------|
    /// | rgb1_to_gray         | ToGrayscale        | 灰度转换                 |
    /// | reduce_domain         | ReduceDomain       | 缩小定义域               |
    /// | threshold             | Threshold          | 阈值分割                 |
    /// | adaptive_threshold    | AdaptiveThreshold  | 自适应阈值分割           |
    /// | connection            | (见CvRegion)       | 连通域分析               |
    /// | dev_display           | CreateDisplayImage | 生成显示用混合图        |
    /// 
    /// 设计原则：
    /// 1. 每个公开方法对应一个Halcon算子，命名和语义对齐
    /// 2. 使用using语句管理Mat生命周期，避免资源泄漏
    /// 3. 不依赖SDK，纯算法封装
    /// </remarks>
    public class CvImage : IDisposable
    {
        #region 私有字段

        private Mat _image;
        private bool _ownsImage;

        #endregion

        #region 构造函数与初始化

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="ownsImage">是否拥有图像所有权（true=Dispose时释放，false=外部管理）</param>
        public CvImage(Mat image, bool ownsImage = false)
        {
            _image = image;
            _ownsImage = ownsImage;
        }

        /// <summary>
        /// 从已有图像创建（克隆副本）
        /// </summary>
        public static CvImage FromImage(Mat image)
        {
            return new CvImage(image.Clone(), ownsImage: true);
        }

        /// <summary>
        /// 创建纯色图像
        /// </summary>
        public static CvImage Create(Size size, Scalar color, int channels = 1)
        {
            var type = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
            var mat = new Mat(size, type, color);
            return new CvImage(mat, ownsImage: true);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 获取底层Mat图像
        /// </summary>
        public Mat Image => _image;

        /// <summary>
        /// 图像尺寸
        /// </summary>
        public Size Size => _image.Size();

        /// <summary>
        /// 通道数
        /// </summary>
        public int Channels => _image.Channels();

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool Empty => _image == null || _image.Empty();

        #endregion

        #region 图像转换

        /// <summary>
        /// 获取图像强度统计 - 等价于Halcon的intensity
        /// </summary>
        /// <param name="mask">定义域掩码（可为null表示整图有效）</param>
        /// <returns>强度统计结果</returns>
        /// <remarks>
        /// Halcon等价:
        ///   intensity(Region, Image, Mean, Deviation)
        /// 返回值包含：灰度直方图、通道均值、标准差
        /// </remarks>
        public ImageStats GetIntensity(Mat? mask = null)
        {
            // 灰度直方图
            using var histogram = new Mat();
            Cv2.CalcHist(
                new[] { _image },
                new[] { 0 },
                mask,
                histogram,
                1,
                new[] { 256 },
                new Rangef[] { new Rangef(0f, 256f) },
                true);

            var histData = new double[256];
            for (int i = 0; i < 256; i++)
                histData[i] = histogram.At<float>(i);

            // 通道均值
            double mean, stdDev;
            double[] channelMeans;

            using var meanMat = new Mat();
            using var stdDevMat = new Mat();
            Cv2.MeanStdDev(_image, meanMat, stdDevMat, mask);

            if (_image.Channels() >= 3)
            {
                var means = meanMat.Get<double>(0);
                channelMeans = new double[] { means, means, means }; // 单值表示整体均值
                stdDev = stdDevMat.Get<double>(0);
                mean = means;
            }
            else
            {
                mean = meanMat.Get<double>(0);
                channelMeans = new double[] { mean };
                stdDev = stdDevMat.Get<double>(0);
            }

            return new ImageStats(histData, channelMeans, mean, stdDev);
        }

        /// <summary>
        /// 转换为灰度图 - 等价于Halcon的rgb1_to_gray
        /// </summary>
        public CvImage ToGrayscale()
        {
            if (_image.Channels() == 1)
            {
                return new CvImage(_image.Clone(), ownsImage: true);
            }

            var gray = new Mat();
            Cv2.CvtColor(_image, gray, ColorConversionCodes.BGR2GRAY);
            return new CvImage(gray, ownsImage: true);
        }

        /// <summary>
        /// 创建显示用混合图像 - 等价于Halcon的dev_display组合
        /// </summary>
        /// <param name="regionImage">纯二值区域图</param>
        /// <param name="mask">定义域掩码（可为null表示整图有效）</param>
        /// <returns>
        /// 显示用混合图：ROI外保持原图，ROI内显示二值化结果
        /// </returns>
        public Mat CreateDisplayImage(Mat regionImage, Mat? mask = null)
        {
            if (mask == null || mask.Empty())
            {
                return regionImage.Clone();
            }

            // 原图 + ROI内二值化结果
            var display = _image.Clone();
            regionImage.CopyTo(display, mask);
            return display;
        }

        #endregion

        #region threshold - 阈值分割

        /// <summary>
        /// 阈值分割 - 等价于Halcon的threshold
        /// </summary>
        /// <param name="threshold">阈值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="type">阈值类型</param>
        /// <param name="mask">定义域掩码（可为null表示整图有效）</param>
        /// <param name="invert">是否反转</param>
        /// <returns>阈值结果结构体</returns>
        /// <remarks>
        /// Halcon等价:
        ///   threshold (ImageReduced, Region, MinGray, MaxGray)
        /// </remarks>
        public ThresholdResult Threshold(
            int threshold,
            int maxValue,
            CvThresholdType type = CvThresholdType.Binary,
            Mat? mask = null,
            bool invert = false)
        {
            // 先对整图做阈值化
            using var fullBinary = new Mat();
            double actualThreshold = Cv2.Threshold(_image, fullBinary, threshold, maxValue,
                CvConvert.ToOpenCvThresholdType(type));

            Mat regionImage;

            if (mask != null && !mask.Empty())
            {
                // ★ 核心：BitwiseAnd 限定域 → ROI外严格为0
                // 等价于 Halcon 的 reduce_domain + threshold 组合效果
                regionImage = new Mat();
                Cv2.BitwiseAnd(fullBinary, fullBinary, regionImage, mask);
            }
            else
            {
                regionImage = fullBinary.Clone();
            }

            // 反转处理
            if (invert)
            {
                var inverted = new Mat();
                Cv2.BitwiseNot(regionImage, inverted);
                regionImage.Dispose();
                regionImage = inverted;
            }

            return new ThresholdResult(regionImage, actualThreshold);
        }

        /// <summary>
        /// 自适应阈值分割 - 等价于Halcon的adaptive_threshold
        /// </summary>
        /// <param name="maxValue">最大值</param>
        /// <param name="adaptiveMethod">自适应方法</param>
        /// <param name="blockSize">邻域大小（奇数）</param>
        /// <param name="c">常数项</param>
        /// <param name="type">基础阈值类型</param>
        /// <param name="mask">定义域掩码（可为null表示整图有效）</param>
        /// <param name="invert">是否反转</param>
        /// <returns>阈值结果结构体</returns>
        public ThresholdResult AdaptiveThreshold(
            int maxValue,
            CvAdaptiveMethod adaptiveMethod,
            int blockSize,
            double c = 5,
            CvThresholdType type = CvThresholdType.Binary,
            Mat? mask = null,
            bool invert = false)
        {
            using var fullBinary = new Mat();
            Cv2.AdaptiveThreshold(
                _image,
                fullBinary,
                maxValue,
                CvConvert.ToOpenCvAdaptiveType(adaptiveMethod),
                CvConvert.ToOpenCvThresholdType(type),
                blockSize,
                c);

            Mat regionImage;

            if (mask != null && !mask.Empty())
            {
                regionImage = new Mat();
                Cv2.BitwiseAnd(fullBinary, fullBinary, regionImage, mask);
            }
            else
            {
                regionImage = fullBinary.Clone();
            }

            if (invert)
            {
                var inverted = new Mat();
                Cv2.BitwiseNot(regionImage, inverted);
                regionImage.Dispose();
                regionImage = inverted;
            }

            return new ThresholdResult(regionImage, actualThreshold: blockSize, threshold: blockSize);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_ownsImage && _image != null)
            {
                _image.Dispose();
            }
            _image = null!;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region 阈值结果结构

        /// <summary>
        /// 阈值分割结果
        /// </summary>
        public readonly struct ThresholdResult : IDisposable
        {
            /// <summary>
            /// 纯二值区域图（ROI外严格为0）
            /// </summary>
            public Mat RegionImage { get; }

            /// <summary>
            /// 实际使用的阈值
            /// </summary>
            public double ActualThreshold { get; }

            /// <summary>
            /// 使用的阈值
            /// </summary>
            public double Threshold { get; }

            internal ThresholdResult(Mat regionImage, double actualThreshold, double threshold = 0)
            {
                RegionImage = regionImage;
                ActualThreshold = actualThreshold;
                Threshold = threshold;
            }

            /// <summary>
            /// 释放资源
            /// </summary>
            public void Dispose()
            {
                RegionImage?.Dispose();
            }
        }

        #endregion
    }
}
