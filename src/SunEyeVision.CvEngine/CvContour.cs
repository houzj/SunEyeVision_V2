using System;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 轮廓数据载体 - 对应 Halcon 的 HXLD
    /// </summary>
    /// <remarks>
    /// 纯数据类，不含任何算法逻辑。
    /// 所有轮廓操作通过 <see cref="CvOp"/> 静态类执行。
    /// 
    /// 设计原则：
    /// 1. 对齐 Halcon HXLD 数据模型
    /// 2. 工厂方法以 Gen_ 前缀（对齐 Halcon gen_* 算子命名）
    /// 3. 支持 IDisposable 生命周期管理
    /// 
    /// 工厂方法对照表：
    /// | Halcon算子           | 本类方法           | 说明           |
    /// |---------------------|--------------------|----------------|
    /// | gen_empty_obj       | GenEmptyObj        | 创建空轮廓     |
    /// | gen_contour_polygon_xld | Create        | 从点集创建轮廓 |
    /// | copy_obj            | CopyObj            | 深拷贝轮廓     |
    /// </remarks>
    public class CvContour : IDisposable
    {
        private Point[]? _points;
        private bool _isClosed;
        private Size _imageSize;

        /// <summary>轮廓点集</summary>
        public Point[]? Points => _points;

        /// <summary>是否闭合轮廓</summary>
        public bool IsClosed => _isClosed;

        /// <summary>所属图像尺寸</summary>
        public Size ImageSize => _imageSize;

        /// <summary>是否为空轮廓</summary>
        public bool IsEmpty => _points == null || _points.Length == 0;

        private CvContour()
        {
            _points = null;
            _isClosed = false;
            _imageSize = new Size(0, 0);
        }

        /// <summary>
        /// 创建空轮廓对象 - 对应 Halcon gen_empty_obj()
        /// </summary>
        public static CvContour GenEmptyObj() => new();

        /// <summary>
        /// 从点集创建轮廓 - 对应 Halcon gen_contour_polygon_xld(Contour, Y, X)
        /// </summary>
        /// <param name="points">轮廓点集</param>
        /// <param name="isClosed">是否闭合</param>
        /// <param name="imageSize">所属图像尺寸</param>
        public static CvContour Create(Point[] points, bool isClosed, Size imageSize)
        {
            return new CvContour
            {
                _points = points,
                _isClosed = isClosed,
                _imageSize = imageSize
            };
        }

        /// <summary>
        /// 深拷贝 - 对应 Halcon copy_obj(Object, Copy, Index1, Index2)
        /// </summary>
        public CvContour CopyObj()
        {
            return new CvContour
            {
                _points = _points?.ToArray(),
                _isClosed = _isClosed,
                _imageSize = _imageSize
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _points = null;
            GC.SuppressFinalize(this);
        }
    }
}
