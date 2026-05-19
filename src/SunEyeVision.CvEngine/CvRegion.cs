using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Imaging;
using WpfRect = System.Windows.Rect;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 区域数据载体 - 对应 Halcon 的 HRegion/HObject
    /// </summary>
    /// <remarks>
    /// 纯数据类，不含任何算法逻辑。
    /// 所有区域操作通过 <see cref="CvOp"/> 静态类执行。
    /// 
    /// 设计原则：
    /// 1. 内部用 List&lt;RegionItem&gt; 存储（对齐 HObject tuple 语义）
    /// 2. Count 属性即数组长度（对齐 Halcon CountObj()）
    /// 3. Mask 属性返回合并掩码（懒加载）
    /// 4. CC 缓存可选，由 Connection 设置，下游算子零开销复用
    /// 
    /// 工厂方法对照表：
    /// | Halcon算子      | 本类方法           | 说明           |
    /// |-----------------|--------------------|----------------|
    /// | gen_empty_obj   | GenEmptyObj        | 创建空区域     |
    /// | gen_rectangle1  | GenRectangle1      | 创建矩形区域   |
    /// | (从二值图创建)  | FromMask           | 从掩码创建区域 |
    /// | (UI区域列表)    | CreateFromRegions  | 从UI定义创建   |
    /// </remarks>
    public class CvRegion : IVisionRegion, IDisposable
    {
        #region 内部字段

        /// <summary>子区域列表（对齐 HObject tuple）</summary>
        internal List<RegionItem> _items;

        /// <summary>共享图像尺寸（所有 item 在同一坐标空间）</summary>
        internal Size _imageSize;

        /// <summary>CC 缓存 - 标签图（可选，由 Connection 设置）</summary>
        internal Mat? _ccLabels;

        /// <summary>CC 缓存 - 连通域统计（可选，由 Connection 设置）</summary>
        internal Mat? _ccStats;

        /// <summary>CC 缓存 - 连通域质心（可选，由 Connection 设置）</summary>
        internal Mat? _ccCentroids;

        /// <summary>合并掩码缓存（懒加载）</summary>
        private Mat? _combinedMaskCache;

        #endregion

        #region 构造函数与工厂方法

        /// <summary>
        /// 私有构造 - 通过工厂方法或 CvOp 内部创建
        /// </summary>
        private CvRegion()
        {
            _items = new List<RegionItem>();
            _imageSize = new Size(0, 0);
        }

        /// <summary>
        /// 内部构造 - 供 CvOp 创建单区域结果（1 个 RegionItem，无 CC 缓存）
        /// </summary>
        /// <param name="mask">合并掩码（CV_8UC1），null 或全黑则创建空区域</param>
        /// <param name="imageSize">图像尺寸</param>
        internal CvRegion(Mat? mask, Size imageSize)
        {
            _items = new List<RegionItem>();
            _imageSize = imageSize;
            if (mask != null && !mask.Empty() && Cv2.CountNonZero(mask) > 0)
            {
                _items.Add(new RegionItem(mask));
            }
        }

        /// <summary>
        /// 内部构造 - 供 CvOp 创建多区域结果（N 个 RegionItem + CC 缓存）
        /// </summary>
        internal CvRegion(List<RegionItem> items, Size imageSize,
            Mat? ccLabels = null, Mat? ccStats = null, Mat? ccCentroids = null)
        {
            _items = items ?? new List<RegionItem>();
            _imageSize = imageSize;
            _ccLabels = ccLabels;
            _ccStats = ccStats;
            _ccCentroids = ccCentroids;
        }

        /// <summary>
        /// 创建空区域对象 - 对应 Halcon gen_empty_obj()
        /// </summary>
        public static CvRegion GenEmptyObj() => new();

        /// <summary>
        /// 创建轴对齐矩形区域 - 对应 Halcon gen_rectangle1(Region, Row1, Col1, Row2, Col2)
        /// </summary>
        public static CvRegion GenRectangle1(int row1, int col1, int row2, int col2)
        {
            int x = Math.Min(col1, col2);
            int y = Math.Min(row1, row2);
            int w = Math.Abs(col2 - col1) + 1;
            int h = Math.Abs(row2 - row1) + 1;

            var imageSize = new Size(x + w, y + h);
            var mask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);
            Cv2.Rectangle(mask, new Rect(x, y, w, h), Scalar.White, -1);

            return new CvRegion(mask, imageSize);
        }

        /// <summary>
        /// 从已有掩码 Mat 创建区域
        /// </summary>
        /// <param name="mask">二值掩码（CV_8UC1, 非零=有效像素）</param>
        /// <param name="ownsData">是否拥有数据所有权</param>
        public static CvRegion FromMask(Mat mask, bool ownsData = false)
        {
            if (mask == null || mask.Empty())
                return GenEmptyObj();

            var m = ownsData ? mask : mask.Clone();
            return new CvRegion(m, mask.Size());
        }

        #endregion

        #region 属性

        /// <summary>
        /// 子区域数量 - 对应 Halcon CountObj()
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 区域尺寸（图像空间范围）
        /// </summary>
        public Size ImageSize => _imageSize;

        /// <summary>
        /// 是否为空区域（不含任何子区域）
        /// </summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>
        /// 区域有效像素数 - 用于日志和统计
        /// </summary>
        public int PixelCount
        {
            get
            {
                var combined = GetCombinedMask();
                return combined != null && !combined.Empty() ? Cv2.CountNonZero(combined) : 0;
            }
        }

        #endregion

        #region IVisionRegion 接口实现

        WpfRect IVisionRegion.BoundingBox
        {
            get
            {
                var mask = GetCombinedMask();
                if (mask == null || mask.Empty())
                    return WpfRect.Empty;
                var rect = Cv2.BoundingRect(mask);
                return new WpfRect(rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        WpfPoint[] IVisionRegion.GetContour()
        {
            var mask = GetCombinedMask();
            if (mask == null || mask.Empty())
                return Array.Empty<WpfPoint>();

            var contours = Cv2.FindContoursAsArray(mask,
                RetrievalModes.External, ContourApproximationModes.ApproxNone);
            if (contours.Length == 0)
                return Array.Empty<WpfPoint>();

            return contours[0].Select(p => new WpfPoint(p.X, p.Y)).ToArray();
        }

        byte[] IVisionRegion.GetMaskData()
        {
            var mask = GetCombinedMask();
            if (mask == null || mask.Empty())
                return Array.Empty<byte>();

            var bytes = new byte[mask.Total() * mask.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(mask.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        IVisionRegion IVisionRegion.Clone() => CopyObj();

        #endregion

        #region 内部访问（仅 CvEngine 内部使用）

        /// <summary>获取组合掩码（懒加载）</summary>
        internal Mat? Mask => GetCombinedMask();

        /// <summary>是否包含 CC 缓存</summary>
        internal bool HasLabels => _ccLabels != null && !_ccLabels.Empty();

        /// <summary>获取标签图 (CV_32SC1)，仅 Connection 产出时有效</summary>
        internal Mat? Labels => _ccLabels;

        /// <summary>获取连通域统计数据</summary>
        internal Mat? Stats => _ccStats;

        /// <summary>获取连通域质心数据</summary>
        internal Mat? Centroids => _ccCentroids;

        /// <summary>
        /// 获取合并掩码（懒加载，缓存）
        /// </summary>
        internal Mat? GetCombinedMask()
        {
            if (_combinedMaskCache != null) return _combinedMaskCache;
            if (_items.Count == 0) return null;

            // 快速路径：有 CC 缓存时直接从 labels 派生
            if (_ccLabels != null && !_ccLabels.Empty())
            {
                _combinedMaskCache = new Mat();
                Cv2.Compare(_ccLabels, 0, _combinedMaskCache, CmpType.GT);
                return _combinedMaskCache;
            }

            // 慢速路径：合并所有 item 的 Mask
            _combinedMaskCache = new Mat(_imageSize, MatType.CV_8UC1, Scalar.Black);
            foreach (var item in _items)
            {
                var mask = item.GetMask();
                if (mask != null && !mask.Empty())
                    Cv2.BitwiseOr(_combinedMaskCache, mask, _combinedMaskCache);
            }
            return _combinedMaskCache;
        }

        #endregion

        #region 生命周期管理 - CopyObj / Dispose

        /// <summary>
        /// 深拷贝 - 对应 Halcon copy_obj()
        /// </summary>
        public CvRegion CopyObj()
        {
            // 强制提取所有 Mask（断开懒加载引用）
            foreach (var item in _items)
                item.GetMask();

            // 克隆 items 的 Mask
            var items = new List<RegionItem>(_items.Count);
            foreach (var item in _items)
            {
                var mask = item.Mask;
                if (mask != null && !mask.Empty())
                    items.Add(new RegionItem(mask.Clone()));
            }

            return new CvRegion(items, _imageSize,
                _ccLabels?.Clone(), _ccStats?.Clone(), _ccCentroids?.Clone());
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            foreach (var item in _items) item.Dispose();
            _items.Clear();
            _combinedMaskCache?.Dispose(); _combinedMaskCache = null;
            _ccLabels?.Dispose(); _ccLabels = null;
            _ccStats?.Dispose(); _ccStats = null;
            _ccCentroids?.Dispose(); _ccCentroids = null;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer 安全网
        /// </summary>
        ~CvRegion()
        {
            foreach (var item in _items) item.Dispose();
            _combinedMaskCache?.Dispose();
            _ccLabels?.Dispose();
            _ccStats?.Dispose();
            _ccCentroids?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// 子区域项 - CvRegion 的内部存储单元，对齐 HObject tuple 中的单个元素
    /// </summary>
    /// <remarks>
    /// Connection 产出时 Mask 懒加载（从 _ccLabels 提取），
    /// Threshold 等产出时直接设置 Mask。
    /// </remarks>
    internal class RegionItem : IDisposable
    {
        private Mat? _mask;

        // 懒加载参数（仅 Connection 产出时设置）
        private readonly Mat? _ccLabelsRef;
        private readonly int _labelIndex;
        private readonly Size _imageSize;

        /// <summary>创建带独立 Mask 的 item（Threshold 等产出）</summary>
        internal RegionItem(Mat mask)
        {
            _mask = mask;
        }

        /// <summary>创建懒加载 Mask 的 item（Connection 产出）</summary>
        internal RegionItem(Mat ccLabelsRef, int labelIndex, Size imageSize)
        {
            _ccLabelsRef = ccLabelsRef;
            _labelIndex = labelIndex;
            _imageSize = imageSize;
        }

        /// <summary>获取独立掩码（不触发懒加载，可能为 null）</summary>
        internal Mat? Mask => _mask;

        /// <summary>
        /// 获取或创建独立掩码（懒加载：首次访问时从 _ccLabels 提取）
        /// </summary>
        internal Mat? GetMask()
        {
            if (_mask != null) return _mask;
            if (_ccLabelsRef == null || _ccLabelsRef.Empty()) return null;

            _mask = new Mat();
            Cv2.Compare(_ccLabelsRef, _labelIndex, _mask, CmpType.EQ);
            return _mask;
        }

        public void Dispose()
        {
            _mask?.Dispose();
            _mask = null;
            // 不释放 _ccLabelsRef，由 CvRegion 拥有
        }
    }
}
