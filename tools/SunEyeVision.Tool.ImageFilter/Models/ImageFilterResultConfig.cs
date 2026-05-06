using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.ImageFilter.Models
{
    /// <summary>
    /// 图像滤波工具结果判断配置
    /// </summary>
    /// <remarks>
    /// 支持输出为空判断和输出均值判断。
    /// 所有判断条件可独立启用/禁用，支持范围配置。
    /// </remarks>
    public class ImageFilterResultConfig : ObservableObject
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

        #region 输出均值判断

        private bool _isMeanCheckEnabled;
        /// <summary>
        /// 是否启用输出均值判断
        /// </summary>
        public bool IsMeanCheckEnabled
        {
            get => _isMeanCheckEnabled;
            set => SetProperty(ref _isMeanCheckEnabled, value, "输出均值判断");
        }

        private double _meanMin = 0;
        /// <summary>
        /// 输出均值最小值
        /// </summary>
        public double MeanMin
        {
            get => _meanMin;
            set => SetProperty(ref _meanMin, value, "输出均值最小值");
        }

        private double _meanMax = 255;
        /// <summary>
        /// 输出均值最大值
        /// </summary>
        public double MeanMax
        {
            get => _meanMax;
            set => SetProperty(ref _meanMax, value, "输出均值最大值");
        }

        #endregion

        /// <summary>
        /// 深拷贝配置
        /// </summary>
        public ImageFilterResultConfig Clone()
        {
            return new ImageFilterResultConfig
            {
                IsEmptyCheckEnabled = _isEmptyCheckEnabled,
                IsMeanCheckEnabled = _isMeanCheckEnabled,
                MeanMin = _meanMin,
                MeanMax = _meanMax
            };
        }
    }
}
