using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.ImageThreshold.Models
{
    /// <summary>
    /// 图像二值化结果判断配置
    /// </summary>
    public class ImageThresholdResultConfig : ObservableObject
    {
        #region 输出为空判断

        private bool _isEmptyCheckEnabled;
        /// <summary>是否启用输出为空判断</summary>
        public bool IsEmptyCheckEnabled
        {
            get => _isEmptyCheckEnabled;
            set => SetProperty(ref _isEmptyCheckEnabled, value, "输出为空判断");
        }

        #endregion

        #region 白色像素比例判断

        private bool _isWhitePixelRatioCheckEnabled;
        /// <summary>是否启用白色像素比例判断</summary>
        public bool IsWhitePixelRatioCheckEnabled
        {
            get => _isWhitePixelRatioCheckEnabled;
            set => SetProperty(ref _isWhitePixelRatioCheckEnabled, value, "白色像素比例判断");
        }

        private double _whitePixelRatioMin = 0;
        /// <summary>白色像素比例最小值(%)</summary>
        public double WhitePixelRatioMin
        {
            get => _whitePixelRatioMin;
            set => SetProperty(ref _whitePixelRatioMin, value, "白色像素比例最小值");
        }

        private double _whitePixelRatioMax = 100;
        /// <summary>白色像素比例最大值(%)</summary>
        public double WhitePixelRatioMax
        {
            get => _whitePixelRatioMax;
            set => SetProperty(ref _whitePixelRatioMax, value, "白色像素比例最大值");
        }

        #endregion

        #region 输出均值判断

        private bool _isMeanCheckEnabled;
        /// <summary>是否启用输出均值判断</summary>
        public bool IsMeanCheckEnabled
        {
            get => _isMeanCheckEnabled;
            set => SetProperty(ref _isMeanCheckEnabled, value, "输出均值判断");
        }

        private double _meanMin = 0;
        /// <summary>输出均值最小值</summary>
        public double MeanMin
        {
            get => _meanMin;
            set => SetProperty(ref _meanMin, value, "输出均值最小值");
        }

        private double _meanMax = 255;
        /// <summary>输出均值最大值</summary>
        public double MeanMax
        {
            get => _meanMax;
            set => SetProperty(ref _meanMax, value, "输出均值最大值");
        }

        #endregion

        #region 输出面积判断

        private bool _isAreaCheckEnabled;
        /// <summary>是否启用输出面积判断</summary>
        public bool IsAreaCheckEnabled
        {
            get => _isAreaCheckEnabled;
            set => SetProperty(ref _isAreaCheckEnabled, value, "输出面积判断");
        }

        private double _areaMin = 0;
        /// <summary>输出面积最小值(像素数)</summary>
        public double AreaMin
        {
            get => _areaMin;
            set => SetProperty(ref _areaMin, value, "输出面积最小值");
        }

        private double _areaMax = 1000000;
        /// <summary>输出面积最大值(像素数)</summary>
        public double AreaMax
        {
            get => _areaMax;
            set => SetProperty(ref _areaMax, value, "输出面积最大值");
        }

        #endregion

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public ImageThresholdResultConfig Clone()
        {
            return new ImageThresholdResultConfig
            {
                IsEmptyCheckEnabled = _isEmptyCheckEnabled,
                IsWhitePixelRatioCheckEnabled = _isWhitePixelRatioCheckEnabled,
                WhitePixelRatioMin = _whitePixelRatioMin,
                WhitePixelRatioMax = _whitePixelRatioMax,
                IsMeanCheckEnabled = _isMeanCheckEnabled,
                MeanMin = _meanMin,
                MeanMax = _meanMax,
                IsAreaCheckEnabled = _isAreaCheckEnabled,
                AreaMin = _areaMin,
                AreaMax = _areaMax
            };
        }
    }
}
