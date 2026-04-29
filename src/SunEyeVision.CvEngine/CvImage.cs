using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 图像数据载体 - 对应 Halcon 的 HImage / Image(-array)
    /// </summary>
    /// <remarks>
    /// 纯数据类，不含任何算法逻辑。
    /// 所有图像操作通过 <see cref="CvOp"/> 静态类执行。
    /// 
    /// 设计原则（对齐 CvRegion）：
    /// 1. 内部用 List&lt;ImageItem&gt; 存储（对齐 HObject tuple 语义）
    /// 2. Count 属性即数组长度（对齐 Halcon CountObj()）
    /// 3. 单图像快捷属性：Image / Size / Channels（Count=1 时零开销）
    /// 
    /// 当前阶段：CvImage 数组语义基础设施就位，
    /// 算子（ReduceDomain/CropDomain/Rgb1ToGray/Intensity）仍按单图像处理，
    /// 后续按需扩展为数组处理。
    /// </remarks>
    public class CvImage : IDisposable
    {
        #region 内部字段

        /// <summary>图像列表（对齐 HObject tuple）</summary>
        internal List<ImageItem> _items;

        #endregion

        #region 构造函数与工厂方法

        /// <summary>私有构造 - 通过工厂方法或 CvOp 内部创建</summary>
        private CvImage()
        {
            _items = new List<ImageItem>();
        }

        /// <summary>
        /// 构造函数 - 创建单图像 CvImage
        /// </summary>
        /// <param name="image">输入 Mat</param>
        /// <param name="ownsImage">是否拥有所有权（true=Dispose时释放）</param>
        public CvImage(Mat image, bool ownsImage = false)
        {
            _items = new List<ImageItem>();
            if (image != null && !image.Empty())
                _items.Add(new ImageItem(image, ownsImage));
        }

        /// <summary>
        /// 内部构造 - 供 CvOp 创建多图像结果
        /// </summary>
        internal CvImage(List<ImageItem> items)
        {
            _items = items ?? new List<ImageItem>();
        }

        /// <summary>从已有图像创建克隆副本</summary>
        public static CvImage FromMat(Mat image) => new(image.Clone(), ownsImage: true);

        /// <summary>创建纯色图像</summary>
        public static CvImage Create(Size size, Scalar color, int channels = 1)
        {
            var type = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
            return new CvImage(new Mat(size, type, color), ownsImage: true);
        }

        /// <summary>
        /// 创建空图像对象 - 对应 Halcon gen_empty_obj()
        /// </summary>
        public static CvImage GenEmptyObj() => new();

        #endregion

        #region 属性

        /// <summary>
        /// 图像数量 - 对应 Halcon CountObj()
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 底层 Mat 图像（单图像快捷访问，Count=1 时使用）
        /// </summary>
        /// <exception cref="InvalidOperationException">空图像时抛出</exception>
        public Mat Image => _items.Count > 0
            ? _items[0].Image
            : throw new InvalidOperationException("空图像对象");

        /// <summary>
        /// 图像尺寸（单图像快捷访问）
        /// </summary>
        public Size Size => Image.Size();

        /// <summary>
        /// 通道数（单图像快捷访问）
        /// </summary>
        public int Channels => Image.Channels();

        /// <summary>
        /// 是否为空（不含任何图像）
        /// </summary>
        public bool Empty => _items.Count == 0;

        #endregion

        #region 生命周期管理

        /// <summary>
        /// 深拷贝 - 对应 Halcon copy_obj()
        /// </summary>
        public CvImage CopyObj()
        {
            var items = new List<ImageItem>(_items.Count);
            foreach (var item in _items)
            {
                if (item.Image != null && !item.Image.Empty())
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
            }
            return new CvImage(items);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            foreach (var item in _items) item.Dispose();
            _items.Clear();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer 安全网
        /// </summary>
        ~CvImage()
        {
            foreach (var item in _items) item.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 图像项 - CvImage 的内部存储单元，对齐 HObject tuple 中的单个元素
    /// </summary>
    internal class ImageItem : IDisposable
    {
        private readonly Mat _image;
        private readonly bool _ownsImage;

        /// <summary>创建图像项</summary>
        /// <param name="image">图像 Mat</param>
        /// <param name="ownsImage">是否拥有所有权</param>
        internal ImageItem(Mat image, bool ownsImage = true)
        {
            _image = image;
            _ownsImage = ownsImage;
        }

        /// <summary>底层 Mat 图像</summary>
        internal Mat Image => _image;

        public void Dispose()
        {
            if (_ownsImage && _image != null) _image.Dispose();
        }
    }
}
