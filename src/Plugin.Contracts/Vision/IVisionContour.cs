using System;

namespace SunEyeVision.Plugin.Contracts.Vision
{
    /// <summary>
    /// 统一轮廓数据接口 - 对齐 Halcon HXLDCont / CvContour
    /// 生命周期由调用方管理，使用完毕必须调用 <see cref="IDisposable.Dispose"/>
    /// </summary>
    /// <remarks>
    /// 点集使用契约层中性的 <see cref="Point2"/>，不使用 WPF 的 System.Windows.Point：
    /// 本程序集为 UI-free 契约层，适配层负责在自己的坐标系内转换。
    /// </remarks>
    public interface IVisionContour : IDisposable
    {
        /// <summary>轮廓点集</summary>
        Point2[] Points { get; }

        /// <summary>是否闭合轮廓</summary>
        bool IsClosed { get; }

        /// <summary>是否为空轮廓</summary>
        bool IsEmpty { get; }

        /// <summary>深拷贝</summary>
        IVisionContour Clone();
    }
}
