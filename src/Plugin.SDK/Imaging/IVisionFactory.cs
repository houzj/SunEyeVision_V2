using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.SDK.Imaging
{
    /// <summary>
    /// 统一工厂接口 - 创建各种 Vision 对象
    /// 不同的底层引擎（CvEngine / Halcon / OpenCv）各自实现此接口
    /// </summary>
    public interface IVisionFactory
    {
        /// <summary>从文件路径加载图像</summary>
        IVisionImage LoadFromFile(string path);

        /// <summary>从 WPF BitmapSource 创建图像</summary>
        IVisionImage LoadFromBitmapSource(BitmapSource source);

        /// <summary>从字节数组创建图像</summary>
        IVisionImage LoadFromBytes(byte[] data, int width, int height, int channels);

        /// <summary>从二值掩码字节数组创建区域</summary>
        IVisionRegion CreateRegionFromMask(byte[] maskData, int width, int height);

        /// <summary>创建轴对齐矩形区域</summary>
        IVisionRegion BuildRectangle(double row1, double col1, double row2, double col2);

        /// <summary>创建圆形区域</summary>
        IVisionRegion BuildCircle(double row, double col, double radius);

        /// <summary>创建多边形区域</summary>
        IVisionRegion BuildPolygon(Point[] points);

        /// <summary>区域并集</summary>
        IVisionRegion Union(IVisionRegion a, IVisionRegion b);

        /// <summary>区域差集</summary>
        IVisionRegion Difference(IVisionRegion a, IVisionRegion b);
    }
}
