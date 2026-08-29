using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.CircleDetect.Models
{
    /// <summary>
    /// 圆查找结果判断配置（对齐 VisionMaster 圆查找）
    /// </summary>
    /// <remarks>
    /// 判定项：半径 / 中心X / 中心Y / 拟合误差（RMSE） / 匹配点数。
    /// 所有判定满足才判 OK，任一不满足判 NG。
    /// </remarks>
    public class CircleDetectResultConfig : ObservableObject
    {
        #region 半径判断

        private bool _isRadiusEnabled;
        /// <summary>
        /// 是否启用半径判断
        /// </summary>
        public bool IsRadiusEnabled
        {
            get => _isRadiusEnabled;
            set => SetProperty(ref _isRadiusEnabled, value, "半径判断");
        }

        private double _radiusMin;
        /// <summary>
        /// 半径最小值（px）
        /// </summary>
        public double RadiusMin
        {
            get => _radiusMin;
            set => SetProperty(ref _radiusMin, value, "半径最小值");
        }

        private double _radiusMax = 1000;
        /// <summary>
        /// 半径最大值（px）
        /// </summary>
        public double RadiusMax
        {
            get => _radiusMax;
            set => SetProperty(ref _radiusMax, value, "半径最大值");
        }

        #endregion

        #region 中心X判断

        private bool _isCenterXEnabled;
        /// <summary>
        /// 是否启用中心X判断
        /// </summary>
        public bool IsCenterXEnabled
        {
            get => _isCenterXEnabled;
            set => SetProperty(ref _isCenterXEnabled, value, "中心X判断");
        }

        private double _centerXMin = -10000;
        /// <summary>
        /// 中心X最小值
        /// </summary>
        public double CenterXMin
        {
            get => _centerXMin;
            set => SetProperty(ref _centerXMin, value, "中心X最小值");
        }

        private double _centerXMax = 10000;
        /// <summary>
        /// 中心X最大值
        /// </summary>
        public double CenterXMax
        {
            get => _centerXMax;
            set => SetProperty(ref _centerXMax, value, "中心X最大值");
        }

        #endregion

        #region 中心Y判断

        private bool _isCenterYEnabled;
        /// <summary>
        /// 是否启用中心Y判断
        /// </summary>
        public bool IsCenterYEnabled
        {
            get => _isCenterYEnabled;
            set => SetProperty(ref _isCenterYEnabled, value, "中心Y判断");
        }

        private double _centerYMin = -10000;
        /// <summary>
        /// 中心Y最小值
        /// </summary>
        public double CenterYMin
        {
            get => _centerYMin;
            set => SetProperty(ref _centerYMin, value, "中心Y最小值");
        }

        private double _centerYMax = 10000;
        /// <summary>
        /// 中心Y最大值
        /// </summary>
        public double CenterYMax
        {
            get => _centerYMax;
            set => SetProperty(ref _centerYMax, value, "中心Y最大值");
        }

        #endregion

        #region 拟合误差判断

        private bool _isRMSEEnabled;
        /// <summary>
        /// 是否启用拟合误差判断
        /// </summary>
        public bool IsRMSEEnabled
        {
            get => _isRMSEEnabled;
            set => SetProperty(ref _isRMSEEnabled, value, "拟合误差判断");
        }

        private double _rmsEMin;
        /// <summary>
        /// 拟合误差最小值（px）
        /// </summary>
        public double RMSEMin
        {
            get => _rmsEMin;
            set => SetProperty(ref _rmsEMin, value, "拟合误差最小值");
        }

        private double _rmsEMax = 1.0;
        /// <summary>
        /// 拟合误差最大值（px）
        /// </summary>
        public double RMSEMax
        {
            get => _rmsEMax;
            set => SetProperty(ref _rmsEMax, value, "拟合误差最大值");
        }

        #endregion

        #region 匹配点数判断

        private bool _isEdgePointCountEnabled;
        /// <summary>
        /// 是否启用匹配点数判断
        /// </summary>
        public bool IsEdgePointCountEnabled
        {
            get => _isEdgePointCountEnabled;
            set => SetProperty(ref _isEdgePointCountEnabled, value, "匹配点数判断");
        }

        private int _edgePointCountMin = 5;
        /// <summary>
        /// 匹配点数最小值
        /// </summary>
        public int EdgePointCountMin
        {
            get => _edgePointCountMin;
            set => SetProperty(ref _edgePointCountMin, value, "匹配点数最小值");
        }

        private int _edgePointCountMax = 1000;
        /// <summary>
        /// 匹配点数最大值
        /// </summary>
        public int EdgePointCountMax
        {
            get => _edgePointCountMax;
            set => SetProperty(ref _edgePointCountMax, value, "匹配点数最大值");
        }

        #endregion
    }
}
