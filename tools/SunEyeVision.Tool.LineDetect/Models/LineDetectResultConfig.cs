using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.LineDetect.Models
{
    /// <summary>
    /// 直线查找结果判断配置（对齐 VisionMaster 直线查找）
    /// </summary>
    /// <remarks>
    /// 判定项：匹配点数 / 角度 / 拟合误差（RMSE）。
    /// 所有判定满足才判 OK，任一不满足判 NG。
    /// </remarks>
    public class LineDetectResultConfig : ObservableObject
    {
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

        #region 角度判断

        private bool _isAngleEnabled;
        /// <summary>
        /// 是否启用角度判断
        /// </summary>
        public bool IsAngleEnabled
        {
            get => _isAngleEnabled;
            set => SetProperty(ref _isAngleEnabled, value, "角度判断");
        }

        private double _angleMin = -180;
        /// <summary>
        /// 角度最小值（度）
        /// </summary>
        public double AngleMin
        {
            get => _angleMin;
            set => SetProperty(ref _angleMin, value, "角度最小值");
        }

        private double _angleMax = 180;
        /// <summary>
        /// 角度最大值（度）
        /// </summary>
        public double AngleMax
        {
            get => _angleMax;
            set => SetProperty(ref _angleMax, value, "角度最大值");
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
    }
}
