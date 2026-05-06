using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.ImageThreshold.Models
{
    /// <summary>
    /// 图像二值化结果判断配置
    /// </summary>
    /// <remarks>
    /// 二值化工具的结果判断聚焦于像素级指标：
    /// - 输出为空判断：检查二值图是否存在
    /// - 白色像素比例判断：检查白像素占全图比例是否在设定范围内
    /// </remarks>
    public class ImageThresholdResultConfig : ObservableObject
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

        private double _whitePixelRatioMin;
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
                WhitePixelRatioMax = _whitePixelRatioMax
            };
        }
    }
}
