using System;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    /// <summary>
    /// 统一轮廓数据接口 - 对齐 Halcon HXLDCont / CvContour
    /// 生命周期由调用方管理，使用完毕必须调用 <see cref="IDisposable.Dispose"/>
    /// </summary>
    public interface IVisionContour : IDisposable
    {
        /// <summary>轮廓点集</summary>
        Point[] Points { get; }

        /// <summary>是否闭合轮廓</summary>
        bool IsClosed { get; }

        /// <summary>是否为空轮廓</summary>
        bool IsEmpty { get; }

        /// <summary>深拷贝</summary>
        IVisionContour Clone();
    }
}
