using System;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    /// <summary>
    /// 统一图像数据接口 - 对齐 Halcon HImage / OpenCV Mat / CvImage
    /// 生命周期由调用方管理，使用完毕必须调用 <see cref="IDisposable.Dispose"/>
    /// </summary>
    public interface IVisionImage : IDisposable
    {
        /// <summary>图像宽度（像素）</summary>
        int Width { get; }

        /// <summary>图像高度（像素）</summary>
        int Height { get; }

        /// <summary>通道数（Gray8=1, Bgr24=3, Rgb24=3）</summary>
        int Channels { get; }

        /// <summary>是否为空图像</summary>
        bool IsEmpty { get; }

        /// <summary>获取像素数据字节数组（用于算法处理）</summary>
        byte[] GetPixelData();

        /// <summary>深拷贝</summary>
        IVisionImage Clone();
    }
}
