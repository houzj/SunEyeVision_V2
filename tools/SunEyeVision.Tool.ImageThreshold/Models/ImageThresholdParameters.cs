using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageThreshold.Models;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 二值化方法枚举
    /// </summary>
    public enum ThresholdMethod
    {
        /// <summary>全局固定阈值</summary>
        Global = 0,

        /// <summary>OTSU 自动阈值</summary>
        OTSU = 1,

        /// <summary>平滑直方图阈值（均值滤波后阈值）</summary>
        SmoothHisto = 2,

        /// <summary>动态阈值（原图 vs 平滑图）</summary>
        Dynamic = 3,

        /// <summary>局部统计阈值</summary>
        LocalStats = 4
    }

    /// <summary>
    /// 图像二值化工具参数
    /// </summary>
    [JsonDerivedType(typeof(ImageThresholdParameters), "ImageThreshold")]
    public class ImageThresholdParameters : RegionParameters
    {
        #region 二值化参数

        /// <summary>
        /// 二值化方法
        /// </summary>
        public ParamValue<ThresholdMethod> Method { get; set; } =
            ParamValue<ThresholdMethod>.CreateConstant(ThresholdMethod.Global, "Method");

        /// <summary>
        /// 阈值(0-255)，仅 Global 模式使用
        /// </summary>
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 亮暗选择
        /// </summary>
        public ParamValue<LightDarkType> LightDark { get; set; } =
            ParamValue<LightDarkType>.CreateConstant(LightDarkType.Light, "LightDark");

        /// <summary>
        /// 是否反转结果
        /// </summary>
        public ParamValue<bool> Invert { get; set; } =
            ParamValue<bool>.CreateConstant(false, "Invert");

        #endregion

        #region 动态/局部阈值参数

        /// <summary>
        /// 动态阈值偏移量
        /// </summary>
        public ParamValue<int> Offset { get; set; } =
            ParamValue<int>.CreateConstant(10, "Offset");

        /// <summary>
        /// 平滑滤波核大小（SmoothHisto / Dynamic 用）
        /// </summary>
        public ParamValue<int> SmoothKernel { get; set; } =
            ParamValue<int>.CreateConstant(9, "SmoothKernel");

        /// <summary>
        /// 局部统计掩码宽度
        /// </summary>
        public ParamValue<int> MaskWidth { get; set; } =
            ParamValue<int>.CreateConstant(11, "MaskWidth");

        /// <summary>
        /// 局部统计掩码高度
        /// </summary>
        public ParamValue<int> MaskHeight { get; set; } =
            ParamValue<int>.CreateConstant(11, "MaskHeight");

        /// <summary>
        /// 标准差缩放系数（LocalStats 用）
        /// </summary>
        public ParamValue<double> StdDevScale { get; set; } =
            ParamValue<double>.CreateConstant(0.5, "StdDevScale");

        /// <summary>
        /// 绝对阈值偏移（LocalStats 用）
        /// </summary>
        public ParamValue<int> AbsThreshold { get; set; } =
            ParamValue<int>.CreateConstant(5, "AbsThreshold");

        #endregion

        #region 位置修正

        private string? _positionCorrectionBindingSource;
        private string? _positionCorrectionBindingDisplayText;

        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        public ParamValue<bool> IsPositionCorrectionEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsPositionCorrectionEnabled");

        /// <summary>
        /// 位置修正绑定源路径
        /// </summary>
        [JsonPropertyOrder(16)]
        [JsonPropertyName("positionCorrectionBindingSource")]
        public string? PositionCorrectionBindingSource
        {
            get => _positionCorrectionBindingSource;
            set => SetProperty(ref _positionCorrectionBindingSource, value);
        }

        /// <summary>
        /// 位置修正绑定显示文本
        /// </summary>
        [JsonPropertyOrder(17)]
        [JsonPropertyName("positionCorrectionBindingDisplayText")]
        public string? PositionCorrectionBindingDisplayText
        {
            get => _positionCorrectionBindingDisplayText;
            set => SetProperty(ref _positionCorrectionBindingDisplayText, value);
        }

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public ImageThresholdResultConfig ResultConfig { get; set; } = new();

        #endregion

        #region 显示样式

        /// <summary>二值图叠加样式（颜色、透明度）</summary>
        public GraphicsStyle BinaryOverlayStyle { get; set; } = new() { Name = "BinaryOverlay", Opacity = 0.5 };

        /// <summary>统计信息样式</summary>
        public TextStyle StatisticsStyle { get; set; } = new() { Name = "Statistics", FontSize = 11.0, PositionX = 10, PositionY = 22 };

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            if (Threshold.Value < 0 || Threshold.Value > 255)
                result.AddError($"阈值必须在 0-255 范围内，当前值: {Threshold.Value}");

            if (MaxValue.Value < 0 || MaxValue.Value > 255)
                result.AddError($"最大值必须在 0-255 范围内，当前值: {MaxValue.Value}");

            if (Offset.Value < 0)
                result.AddError($"偏移量不能为负数，当前值: {Offset.Value}");

            if (SmoothKernel.Value < 3 || SmoothKernel.Value % 2 == 0)
                result.AddError($"平滑核大小必须为 ≥3 的奇数，当前值: {SmoothKernel.Value}");

            if (MaskWidth.Value < 3 || MaskWidth.Value % 2 == 0)
                result.AddError($"掩码宽度必须为 ≥3 的奇数，当前值: {MaskWidth.Value}");

            if (MaskHeight.Value < 3 || MaskHeight.Value % 2 == 0)
                result.AddError($"掩码高度必须为 ≥3 的奇数，当前值: {MaskHeight.Value}");

            if (StdDevScale.Value < 0)
                result.AddError($"标准差缩放系数不能为负数，当前值: {StdDevScale.Value}");

            if (AbsThreshold.Value < 0)
                result.AddError($"绝对阈值偏移不能为负数，当前值: {AbsThreshold.Value}");

            ValidateResultConfig(result);

            return result;
        }

        private void ValidateResultConfig(ValidationResult result)
        {
            if (ResultConfig == null)
            {
                result.AddError("结果判断配置不能为空");
                return;
            }

            if (ResultConfig.IsWhitePixelRatioCheckEnabled)
            {
                if (ResultConfig.WhitePixelRatioMin >= ResultConfig.WhitePixelRatioMax)
                    result.AddError($"白色像素比例最小值({ResultConfig.WhitePixelRatioMin})必须小于最大值({ResultConfig.WhitePixelRatioMax})");
            }
        }
    }
}
