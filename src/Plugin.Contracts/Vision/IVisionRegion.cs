using System;

namespace SunEyeVision.Plugin.Contracts.Vision
{
    /// <summary>
    /// 统一区域数据接口 - 对齐 Halcon HRegion / CvRegion
    /// 生命周期由调用方管理，使用完毕必须调用 <see cref="IDisposable.Dispose"/>
    /// </summary>
    /// <remarks>
    /// 外接矩形与轮廓点集使用契约层中性的 <see cref="Rect2"/> / <see cref="Point2"/>，
    /// 不使用 WPF 的 System.Windows.Rect/Point：本程序集为 UI-free 契约层。
    /// </remarks>
    public interface IVisionRegion : IDisposable
    {
        /// <summary>区域有效像素数</summary>
        int PixelCount { get; }

        /// <summary>区域外接矩形（图像坐标系）</summary>
        Rect2 BoundingBox { get; }

        /// <summary>是否为空区域</summary>
        bool IsEmpty { get; }

        /// <summary>获取区域外轮廓点集</summary>
        Point2[] GetContour();

        /// <summary>获取二值掩码字节数组（非零=有效像素）</summary>
        byte[] GetMaskData();

        /// <summary>深拷贝</summary>
        IVisionRegion Clone();
    }
}
