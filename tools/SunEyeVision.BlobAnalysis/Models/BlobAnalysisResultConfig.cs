using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.BlobAnalysis.Models
{
    /// <summary>
    /// Blob分析结果判断配置
    /// </summary>
    /// <remarks>
    /// 支持 Blob 数量、总面积、白色像素比例等判断条件，
    /// 用于评估分析结果是否符合预期。
    /// </remarks>
    public class BlobAnalysisResultConfig : ObservableObject
    {
        #region 输出为空判断

        private bool _isEmptyCheckEnabled;
        /// <summary>
        /// 是否启用输出为空判断
        /// </summary>
        public bool IsEmptyCheckEnabled
        {
            get => _isEmptyCheckEnabled;
            set => SetProperty(ref _isEmptyCheckEnabled, value, "输出为空判断");
        }

        #endregion

        #region 白色像素比例判断

        private bool _isWhitePixelRatioCheckEnabled;
        /// <summary>
        /// 是否启用白色像素比例判断
        /// </summary>
        public bool IsWhitePixelRatioCheckEnabled
        {
            get => _isWhitePixelRatioCheckEnabled;
            set => SetProperty(ref _isWhitePixelRatioCheckEnabled, value, "白色像素比例判断");
        }

        private double _whitePixelRatioMin = 0;
        /// <summary>
        /// 白色像素比例最小值(%)
        /// </summary>
        public double WhitePixelRatioMin
        {
            get => _whitePixelRatioMin;
            set => SetProperty(ref _whitePixelRatioMin, value, "白色像素比例最小值");
        }

        private double _whitePixelRatioMax = 100;
        /// <summary>
        /// 白色像素比例最大值(%)
        /// </summary>
        public double WhitePixelRatioMax
        {
            get => _whitePixelRatioMax;
            set => SetProperty(ref _whitePixelRatioMax, value, "白色像素比例最大值");
        }

        #endregion

        #region Blob数量判断

        private bool _isBlobCountCheckEnabled;
        /// <summary>
        /// 是否启用Blob数量判断
        /// </summary>
        public bool IsBlobCountCheckEnabled
        {
            get => _isBlobCountCheckEnabled;
            set => SetProperty(ref _isBlobCountCheckEnabled, value, "Blob数量判断");
        }

        private int _blobCountMin = 0;
        /// <summary>
        /// Blob数量最小值
        /// </summary>
        public int BlobCountMin
        {
            get => _blobCountMin;
            set => SetProperty(ref _blobCountMin, value, "Blob数量最小值");
        }

        private int _blobCountMax = 9999;
        /// <summary>
        /// Blob数量最大值
        /// </summary>
        public int BlobCountMax
        {
            get => _blobCountMax;
            set => SetProperty(ref _blobCountMax, value, "Blob数量最大值");
        }

        #endregion

        #region 总面积判断

        private bool _isTotalAreaCheckEnabled;
        /// <summary>
        /// 是否启用总面积判断
        /// </summary>
        public bool IsTotalAreaCheckEnabled
        {
            get => _isTotalAreaCheckEnabled;
            set => SetProperty(ref _isTotalAreaCheckEnabled, value, "总面积判断");
        }

        private double _totalAreaMin = 0;
        /// <summary>
        /// 总面积最小值(像素数)
        /// </summary>
        public double TotalAreaMin
        {
            get => _totalAreaMin;
            set => SetProperty(ref _totalAreaMin, value, "总面积最小值");
        }

        private double _totalAreaMax = 10000000;
        /// <summary>
        /// 总面积最大值(像素数)
        /// </summary>
        public double TotalAreaMax
        {
            get => _totalAreaMax;
            set => SetProperty(ref _totalAreaMax, value, "总面积最大值");
        }

        #endregion

        #region 平均面积判断

        private bool _isAverageAreaCheckEnabled;
        /// <summary>是否启用平均面积判断</summary>
        public bool IsAverageAreaCheckEnabled
        {
            get => _isAverageAreaCheckEnabled;
            set => SetProperty(ref _isAverageAreaCheckEnabled, value, "平均面积判断");
        }

        private double _averageAreaMin = 0;
        /// <summary>平均面积最小值(像素数)</summary>
        public double AverageAreaMin
        {
            get => _averageAreaMin;
            set => SetProperty(ref _averageAreaMin, value, "平均面积最小值");
        }

        private double _averageAreaMax = 10000000;
        /// <summary>平均面积最大值(像素数)</summary>
        public double AverageAreaMax
        {
            get => _averageAreaMax;
            set => SetProperty(ref _averageAreaMax, value, "平均面积最大值");
        }

        #endregion

        #region 最大面积判断

        private bool _isMaxAreaCheckEnabled;
        /// <summary>是否启用最大面积判断</summary>
        public bool IsMaxAreaCheckEnabled
        {
            get => _isMaxAreaCheckEnabled;
            set => SetProperty(ref _isMaxAreaCheckEnabled, value, "最大面积判断");
        }

        private double _maxAreaMin = 0;
        /// <summary>最大面积最小值(像素数)</summary>
        public double MaxAreaMin
        {
            get => _maxAreaMin;
            set => SetProperty(ref _maxAreaMin, value, "最大面积最小值");
        }

        private double _maxAreaMax = 10000000;
        /// <summary>最大面积最大值(像素数)</summary>
        public double MaxAreaMax
        {
            get => _maxAreaMax;
            set => SetProperty(ref _maxAreaMax, value, "最大面积最大值");
        }

        #endregion

        #region 最小面积判断

        private bool _isMinAreaCheckEnabled;
        /// <summary>是否启用最小面积判断</summary>
        public bool IsMinAreaCheckEnabled
        {
            get => _isMinAreaCheckEnabled;
            set => SetProperty(ref _isMinAreaCheckEnabled, value, "最小面积判断");
        }

        private double _minAreaMin = 0;
        /// <summary>最小面积最小值(像素数)</summary>
        public double MinAreaMin
        {
            get => _minAreaMin;
            set => SetProperty(ref _minAreaMin, value, "最小面积最小值");
        }

        private double _minAreaMax = 10000000;
        /// <summary>最小面积最大值(像素数)</summary>
        public double MinAreaMax
        {
            get => _minAreaMax;
            set => SetProperty(ref _minAreaMax, value, "最小面积最大值");
        }

        #endregion

        #region 平均圆度判断

        private bool _isAverageCircularityCheckEnabled;
        /// <summary>是否启用平均圆度判断</summary>
        public bool IsAverageCircularityCheckEnabled
        {
            get => _isAverageCircularityCheckEnabled;
            set => SetProperty(ref _isAverageCircularityCheckEnabled, value, "平均圆度判断");
        }

        private double _averageCircularityMin = 0;
        /// <summary>平均圆度最小值(0~1)</summary>
        public double AverageCircularityMin
        {
            get => _averageCircularityMin;
            set => SetProperty(ref _averageCircularityMin, value, "平均圆度最小值");
        }

        private double _averageCircularityMax = 1.0;
        /// <summary>平均圆度最大值(0~1)</summary>
        public double AverageCircularityMax
        {
            get => _averageCircularityMax;
            set => SetProperty(ref _averageCircularityMax, value, "平均圆度最大值");
        }

        #endregion

        #region 平均矩形度判断

        private bool _isAverageRectangularityCheckEnabled;
        /// <summary>是否启用平均矩形度判断</summary>
        public bool IsAverageRectangularityCheckEnabled
        {
            get => _isAverageRectangularityCheckEnabled;
            set => SetProperty(ref _isAverageRectangularityCheckEnabled, value, "平均矩形度判断");
        }

        private double _averageRectangularityMin = 0;
        /// <summary>平均矩形度最小值(0~1)</summary>
        public double AverageRectangularityMin
        {
            get => _averageRectangularityMin;
            set => SetProperty(ref _averageRectangularityMin, value, "平均矩形度最小值");
        }

        private double _averageRectangularityMax = 1.0;
        /// <summary>平均矩形度最大值(0~1)</summary>
        public double AverageRectangularityMax
        {
            get => _averageRectangularityMax;
            set => SetProperty(ref _averageRectangularityMax, value, "平均矩形度最大值");
        }

        #endregion

        #region Per-Blob判断（独立blob条件，控制单个blob显示颜色）

        // ===== 每个Blob面积判断 =====

        private bool _isPerBlobAreaCheckEnabled;
        /// <summary>是否启用每个Blob面积判断</summary>
        public bool IsPerBlobAreaCheckEnabled
        {
            get => _isPerBlobAreaCheckEnabled;
            set => SetProperty(ref _isPerBlobAreaCheckEnabled, value, "每个Blob面积判断");
        }

        private double _perBlobAreaMin;
        /// <summary>每个Blob面积最小值(像素数)</summary>
        public double PerBlobAreaMin
        {
            get => _perBlobAreaMin;
            set => SetProperty(ref _perBlobAreaMin, value, "每个Blob面积最小值");
        }

        private double _perBlobAreaMax = 10000000;
        /// <summary>每个Blob面积最大值(像素数)</summary>
        public double PerBlobAreaMax
        {
            get => _perBlobAreaMax;
            set => SetProperty(ref _perBlobAreaMax, value, "每个Blob面积最大值");
        }

        // ===== 每个Blob圆度判断 =====

        private bool _isPerBlobCircularityCheckEnabled;
        /// <summary>是否启用每个Blob圆度判断</summary>
        public bool IsPerBlobCircularityCheckEnabled
        {
            get => _isPerBlobCircularityCheckEnabled;
            set => SetProperty(ref _isPerBlobCircularityCheckEnabled, value, "每个Blob圆度判断");
        }

        private double _perBlobCircularityMin;
        /// <summary>每个Blob圆度最小值(0~1)</summary>
        public double PerBlobCircularityMin
        {
            get => _perBlobCircularityMin;
            set => SetProperty(ref _perBlobCircularityMin, value, "每个Blob圆度最小值");
        }

        private double _perBlobCircularityMax = 1.0;
        /// <summary>每个Blob圆度最大值(0~1)</summary>
        public double PerBlobCircularityMax
        {
            get => _perBlobCircularityMax;
            set => SetProperty(ref _perBlobCircularityMax, value, "每个Blob圆度最大值");
        }

        // ===== 每个Blob矩形度判断 =====

        private bool _isPerBlobRectangularityCheckEnabled;
        /// <summary>是否启用每个Blob矩形度判断</summary>
        public bool IsPerBlobRectangularityCheckEnabled
        {
            get => _isPerBlobRectangularityCheckEnabled;
            set => SetProperty(ref _isPerBlobRectangularityCheckEnabled, value, "每个Blob矩形度判断");
        }

        private double _perBlobRectangularityMin;
        /// <summary>每个Blob矩形度最小值(0~1)</summary>
        public double PerBlobRectangularityMin
        {
            get => _perBlobRectangularityMin;
            set => SetProperty(ref _perBlobRectangularityMin, value, "每个Blob矩形度最小值");
        }

        private double _perBlobRectangularityMax = 1.0;
        /// <summary>每个Blob矩形度最大值(0~1)</summary>
        public double PerBlobRectangularityMax
        {
            get => _perBlobRectangularityMax;
            set => SetProperty(ref _perBlobRectangularityMax, value, "每个Blob矩形度最大值");
        }

        #endregion

        /// <summary>
        /// 是否有任何 per-blob 判断条件启用
        /// </summary>
        public bool HasPerBlobJudgmentEnabled =>
            _isPerBlobAreaCheckEnabled ||
            _isPerBlobCircularityCheckEnabled ||
            _isPerBlobRectangularityCheckEnabled;

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public BlobAnalysisResultConfig Clone()
        {
            return new BlobAnalysisResultConfig
            {
                IsEmptyCheckEnabled = _isEmptyCheckEnabled,
                IsWhitePixelRatioCheckEnabled = _isWhitePixelRatioCheckEnabled,
                WhitePixelRatioMin = _whitePixelRatioMin,
                WhitePixelRatioMax = _whitePixelRatioMax,
                IsBlobCountCheckEnabled = _isBlobCountCheckEnabled,
                BlobCountMin = _blobCountMin,
                BlobCountMax = _blobCountMax,
                IsTotalAreaCheckEnabled = _isTotalAreaCheckEnabled,
                TotalAreaMin = _totalAreaMin,
                TotalAreaMax = _totalAreaMax,
                IsAverageAreaCheckEnabled = _isAverageAreaCheckEnabled,
                AverageAreaMin = _averageAreaMin,
                AverageAreaMax = _averageAreaMax,
                IsMaxAreaCheckEnabled = _isMaxAreaCheckEnabled,
                MaxAreaMin = _maxAreaMin,
                MaxAreaMax = _maxAreaMax,
                IsMinAreaCheckEnabled = _isMinAreaCheckEnabled,
                MinAreaMin = _minAreaMin,
                MinAreaMax = _minAreaMax,
                IsAverageCircularityCheckEnabled = _isAverageCircularityCheckEnabled,
                AverageCircularityMin = _averageCircularityMin,
                AverageCircularityMax = _averageCircularityMax,
                IsAverageRectangularityCheckEnabled = _isAverageRectangularityCheckEnabled,
                AverageRectangularityMin = _averageRectangularityMin,
                AverageRectangularityMax = _averageRectangularityMax,
                IsPerBlobAreaCheckEnabled = _isPerBlobAreaCheckEnabled,
                PerBlobAreaMin = _perBlobAreaMin,
                PerBlobAreaMax = _perBlobAreaMax,
                IsPerBlobCircularityCheckEnabled = _isPerBlobCircularityCheckEnabled,
                PerBlobCircularityMin = _perBlobCircularityMin,
                PerBlobCircularityMax = _perBlobCircularityMax,
                IsPerBlobRectangularityCheckEnabled = _isPerBlobRectangularityCheckEnabled,
                PerBlobRectangularityMin = _perBlobRectangularityMin,
                PerBlobRectangularityMax = _perBlobRectangularityMax
            };
        }
    }
}
