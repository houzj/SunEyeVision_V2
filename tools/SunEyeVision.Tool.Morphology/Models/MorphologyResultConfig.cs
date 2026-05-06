using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Morphology.Models
{
    /// <summary>
    /// 形态学工具结果判断配置
    /// </summary>
    /// <remarks>
    /// 支持输出为空、总面积、Blob数量、最大Blob面积等判断条件。
    /// 所有判断条件可独立启用/禁用，支持范围配置。
    /// </remarks>
    public class MorphologyResultConfig : ObservableObject
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

        #region 总面积判断

        private bool _isAreaCheckEnabled;
        /// <summary>
        /// 是否启用总面积判断
        /// </summary>
        public bool IsAreaCheckEnabled
        {
            get => _isAreaCheckEnabled;
            set => SetProperty(ref _isAreaCheckEnabled, value, "总面积判断");
        }

        private double _areaMin;
        /// <summary>
        /// 总面积最小值
        /// </summary>
        public double AreaMin
        {
            get => _areaMin;
            set => SetProperty(ref _areaMin, value, "总面积最小值");
        }

        private double _areaMax = 1000000;
        /// <summary>
        /// 总面积最大值
        /// </summary>
        public double AreaMax
        {
            get => _areaMax;
            set => SetProperty(ref _areaMax, value, "总面积最大值");
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

        private int _blobCountMin;
        /// <summary>
        /// Blob数量最小值
        /// </summary>
        public int BlobCountMin
        {
            get => _blobCountMin;
            set => SetProperty(ref _blobCountMin, value, "Blob数量最小值");
        }

        private int _blobCountMax = 1000;
        /// <summary>
        /// Blob数量最大值
        /// </summary>
        public int BlobCountMax
        {
            get => _blobCountMax;
            set => SetProperty(ref _blobCountMax, value, "Blob数量最大值");
        }

        #endregion

        #region 最大Blob面积判断

        private bool _isMaxBlobAreaCheckEnabled;
        /// <summary>
        /// 是否启用最大Blob面积判断
        /// </summary>
        public bool IsMaxBlobAreaCheckEnabled
        {
            get => _isMaxBlobAreaCheckEnabled;
            set => SetProperty(ref _isMaxBlobAreaCheckEnabled, value, "最大Blob面积判断");
        }

        private double _maxBlobAreaMin;
        /// <summary>
        /// 最大Blob面积最小值
        /// </summary>
        public double MaxBlobAreaMin
        {
            get => _maxBlobAreaMin;
            set => SetProperty(ref _maxBlobAreaMin, value, "最大Blob面积最小值");
        }

        private double _maxBlobAreaMax = 1000000;
        /// <summary>
        /// 最大Blob面积最大值
        /// </summary>
        public double MaxBlobAreaMax
        {
            get => _maxBlobAreaMax;
            set => SetProperty(ref _maxBlobAreaMax, value, "最大Blob面积最大值");
        }

        #endregion

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public MorphologyResultConfig Clone()
        {
            return new MorphologyResultConfig
            {
                IsEmptyCheckEnabled = _isEmptyCheckEnabled,
                IsAreaCheckEnabled = _isAreaCheckEnabled,
                AreaMin = _areaMin,
                AreaMax = _areaMax,
                IsBlobCountCheckEnabled = _isBlobCountCheckEnabled,
                BlobCountMin = _blobCountMin,
                BlobCountMax = _blobCountMax,
                IsMaxBlobAreaCheckEnabled = _isMaxBlobAreaCheckEnabled,
                MaxBlobAreaMin = _maxBlobAreaMin,
                MaxBlobAreaMax = _maxBlobAreaMax
            };
        }
    }
}
