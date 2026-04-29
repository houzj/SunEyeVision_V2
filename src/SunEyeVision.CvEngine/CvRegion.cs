using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// 区域数据载体 - 对应 Halcon 的 HRegion/HObject
    /// </summary>
    /// <remarks>
    /// 纯不可变数据类，不含任何算法逻辑。
    /// 所有区域操作通过 <see cref="CvOp"/> 静态类执行。
    /// 
    /// 设计原则：
    /// 1. 内部可包含 0~N 个子区域（对应 Halcon 的 CountObj()）
    /// 2. 不暴露底层 Mat（无 RegionImage 属性）
    /// 3. 工厂方法以 Gen_ 前缀（对齐 Halcon gen_* 算子命名）
    /// 
    /// 工厂方法对照表：
    /// | Halcon算子      | 本类方法           | 说明           |
    /// |-----------------|--------------------|----------------|
    /// | gen_empty_obj   | GenEmptyObj        | 创建空区域     |
    /// | gen_rectangle1  | GenRectangle1      | 创建矩形区域   |
    /// | (从二值图创建)  | FromMask           | 从掩码创建区域 |
    /// | (UI区域列表)    | CreateFromRegions  | 从UI定义创建   |
    /// </remarks>
    public class CvRegion : IDisposable
    {
        #region 内部字段（同程序集内 CvOp 访问）

        internal Mat? _mask;        // 合并二值掩码 (CV_8UC1)，始终可用
        internal Mat? _labels;      // 标签图 (CV_32SC1)，0=背景, 1..N=子区域，仅多区域时存在
        internal Mat? _stats;       // 连通域统计 (ConnectedComponentsWithStats 输出)
        internal Mat? _centroids;   // 连通域质心 (ConnectedComponentsWithStats 输出)
        internal int _count;        // 子区域数量 (对应 Halcon CountObj)
        internal Size _imageSize;   // 图像尺寸

        #endregion

        #region 构造函数与工厂方法

        /// <summary>
        /// 私有构造 - 通过工厂方法或 CvOp 内部创建
        /// </summary>
        private CvRegion()
        {
            _count = 0;
            _imageSize = new Size(0, 0);
        }

        /// <summary>
        /// 内部构造函数 - 供 CvOp 构建单区域计算结果（无标签信息）
        /// </summary>
        internal CvRegion(Mat? mask, int count, Size imageSize)
        {
            _mask = mask;
            _count = count;
            _imageSize = imageSize;
        }

        /// <summary>
        /// 内部构造函数 - 供 CvOp 构建多区域计算结果（含完整连通域分析数据）
        /// </summary>
        internal CvRegion(Mat? mask, Mat labels, Mat stats, Mat centroids, int count, Size imageSize)
        {
            _mask = mask;
            _labels = labels;
            _stats = stats;
            _centroids = centroids;
            _count = count;
            _imageSize = imageSize;
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
            var region = new CvRegion();
            int x = Math.Min(col1, col2);
            int y = Math.Min(row1, row2);
            int w = Math.Abs(col2 - col1) + 1;
            int h = Math.Abs(row2 - row1) + 1;

            region._imageSize = new Size(x + w, y + h);
            region._mask = new Mat(region._imageSize, MatType.CV_8UC1, Scalar.Black);
            Cv2.Rectangle(region._mask, new Rect(x, y, w, h), Scalar.White, -1);
            region._count = 1;
            return region;
        }

        /// <summary>
        /// 从已有掩码 Mat 创建区域 - 对应从二值图像生成区域
        /// </summary>
        /// <param name="mask">二值掩码（CV_8UC1, 非零=有效像素）</param>
        /// <param name="ownsData">是否拥有数据所有权</param>
        public static CvRegion FromMask(Mat mask, bool ownsData = false)
        {
            var region = new CvRegion();
            if (mask == null || mask.Empty())
                return region;

            region._imageSize = mask.Size();
            region._mask = ownsData ? mask : mask.Clone();
            region._count = Cv2.CountNonZero(mask) > 0 ? 1 : 0;
            return region;
        }

        /// <summary>
        /// 从 UI 区域定义列表直接创建 CvRegion（组合 RegionMaskBuilder + FromMask）
        /// </summary>
        /// <param name="imageSize">图像尺寸</param>
        /// <param name="regions">UI 区域定义列表（至少包含一个有效区域）</param>
        /// <exception cref="ArgumentNullException">regions 为 null</exception>
        /// <exception cref="ArgumentException">regions 为空或无有效区域</exception>
        public static CvRegion CreateFromRegions(Size imageSize, IEnumerable<RegionData> regions)
        {
            if (regions == null)
                throw new ArgumentNullException(nameof(regions));

            var regionList = regions.ToList();
            if (regionList.Count == 0)
                throw new ArgumentException("区域列表不能为空", nameof(regions));

            var validRegions = regionList.Where(r => r.IsEnabled && r.Parameters != null).ToList();
            if (validRegions.Count == 0)
                throw new ArgumentException("区域列表中无有效（启用且参数完整）的区域", nameof(regions));

            var mask = RegionMaskBuilder.CreateMask(imageSize, validRegions);
            return FromMask(mask, ownsData: true);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 子区域数量 - 对应 Halcon CountObj()
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// 区域尺寸（图像空间范围）
        /// </summary>
        public Size ImageSize => _imageSize;

        /// <summary>
        /// 是否为空区域（不含任何子区域）
        /// </summary>
        public bool IsEmpty => _count == 0 || _mask == null || _mask.Empty();

        /// <summary>
        /// 区域有效像素数 - 用于日志和统计，无需暴露底层 Mat
        /// </summary>
        public int PixelCount => (_mask != null && !_mask.Empty()) ? Cv2.CountNonZero(_mask) : 0;

        #endregion

        #region 内部访问（仅 CvEngine 内部使用）

        /// <summary>获取组合掩码</summary>
        internal Mat? Mask => _mask;

        /// <summary>是否包含子区域标签信息（可按索引访问子区域）</summary>
        internal bool HasLabels => _labels != null && !_labels.Empty();

        /// <summary>获取标签图 (CV_32SC1)，仅多区域结果有效</summary>
        internal Mat? Labels => _labels;

        /// <summary>获取连通域统计数据，仅多区域结果有效</summary>
        internal Mat? Stats => _stats;

        /// <summary>获取连通域质心数据，仅多区域结果有效</summary>
        internal Mat? Centroids => _centroids;

        #endregion

        #region 生命周期管理 - CopyObj / Dispose

        /// <summary>
        /// 深拷贝 - 对应 Halcon copy_obj(Object, Copy, Index1, Index2)
        /// 同时拷贝 labels/stats/centroids（如有）
        /// </summary>
        public CvRegion CopyObj()
        {
            var copy = GenEmptyObj();
            copy._imageSize = _imageSize;
            copy._mask = _mask?.Clone();
            copy._labels = _labels?.Clone();
            copy._stats = _stats?.Clone();
            copy._centroids = _centroids?.Clone();
            copy._count = _count;
            return copy;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _mask?.Dispose(); _mask = null;
            _labels?.Dispose(); _labels = null;
            _stats?.Dispose(); _stats = null;
            _centroids?.Dispose(); _centroids = null;
            _count = 0;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer 安全网
        /// </summary>
        ~CvRegion()
        {
            _mask?.Dispose();
            _labels?.Dispose();
            _stats?.Dispose();
            _centroids?.Dispose();
        }

        #endregion
    }
}
