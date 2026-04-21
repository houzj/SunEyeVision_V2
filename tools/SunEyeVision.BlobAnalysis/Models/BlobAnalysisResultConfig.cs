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
                TotalAreaMax = _totalAreaMax
            };
        }
    }
}
