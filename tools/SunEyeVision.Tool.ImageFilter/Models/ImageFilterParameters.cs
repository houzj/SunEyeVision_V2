using System.Collections.Generic;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.ImageFilter.Models
{
    public enum FilterType
    {
        Gaussian = 0,
        Median = 1,
        Mean = 2,
        Bilateral = 3,
        Guided = 4
    }

    public enum MaskShape
    {
        Circle = 0,
        Square = 1
    }

    [JsonDerivedType(typeof(ImageFilterParameters), "ImageFilter")]
    public class ImageFilterParameters : RegionParameters
    {
        public ParamValue<Models.FilterType> FilterType { get; set; } = ParamValue<Models.FilterType>.CreateConstant(Models.FilterType.Gaussian, "FilterType");
        public ParamValue<int> KernelSize { get; set; } = ParamValue<int>.CreateConstant(5, "KernelSize");
        public ParamValue<double> Sigma { get; set; } = ParamValue<double>.CreateConstant(1.5, "Sigma");
        public ParamValue<MaskShape> MaskType { get; set; } = ParamValue<MaskShape>.CreateConstant(MaskShape.Circle, "MaskType");
        public ParamValue<int> SigmaSpatial { get; set; } = ParamValue<int>.CreateConstant(15, "SigmaSpatial");
        public ParamValue<int> SigmaRange { get; set; } = ParamValue<int>.CreateConstant(75, "SigmaRange");
        public ParamValue<int> GuidedRadius { get; set; } = ParamValue<int>.CreateConstant(5, "GuidedRadius");
        public ParamValue<double> GuidedAmplitude { get; set; } = ParamValue<double>.CreateConstant(0.01, "GuidedAmplitude");

        #region 位置修正

        private string? _positionCorrectionBindingSource;
        private string? _positionCorrectionBindingDisplayText;

        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        public ParamValue<bool> IsPositionCorrectionEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsPositionCorrectionEnabled");

        /// <summary>
        /// 位置修正绑定源路径（可序列化）
        /// </summary>
        [JsonPropertyOrder(16)]
        [JsonPropertyName("positionCorrectionBindingSource")]
        public string? PositionCorrectionBindingSource
        {
            get => _positionCorrectionBindingSource;
            set => SetProperty(ref _positionCorrectionBindingSource, value);
        }

        /// <summary>
        /// 位置修正绑定显示文本（可序列化）
        /// </summary>
        [JsonPropertyOrder(17)]
        [JsonPropertyName("positionCorrectionBindingDisplayText")]
        public string? PositionCorrectionBindingDisplayText
        {
            get => _positionCorrectionBindingDisplayText;
            set => SetProperty(ref _positionCorrectionBindingDisplayText, value);
        }

        #endregion

        #region 显示样式

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public ImageFilterResultConfig ResultConfig { get; set; } = new();

        /// <summary>
        /// 叠加显示样式
        /// </summary>
        public GraphicsStyle OverlayStyle { get; set; } = new() { Name = "Overlay", LineWidth = 2 };

        /// <summary>
        /// 统计信息样式
        /// </summary>
        public TextStyle StatisticsStyle { get; set; } = new() { Name = "Statistics", FontSize = 11.0, PositionX = 10, PositionY = 22 };

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            // 滤波参数验证
            if (KernelSize.Value < 1)
                result.AddError($"KernelSize must be >0, current: {KernelSize.Value}");
            if (Sigma.Value < 0)
                result.AddError($"Sigma cannot be negative, current: {Sigma.Value}");
            if (SigmaSpatial.Value < 0)
                result.AddError($"SigmaSpatial cannot be negative, current: {SigmaSpatial.Value}");
            if (SigmaRange.Value < 0)
                result.AddError($"SigmaRange cannot be negative, current: {SigmaRange.Value}");
            if (GuidedRadius.Value < 1)
                result.AddError($"GuidedRadius must be >0, current: {GuidedRadius.Value}");
            if (GuidedAmplitude.Value <= 0)
                result.AddError($"GuidedAmplitude must be >0, current: {GuidedAmplitude.Value}");

            // 结果配置验证
            ValidateResultConfig(result);

            return result;
        }

        /// <summary>
        /// 验证结果判断配置
        /// </summary>
        private void ValidateResultConfig(ValidationResult result)
        {
            if (ResultConfig == null)
            {
                result.AddError("ResultConfig cannot be null");
                return;
            }

            if (ResultConfig.IsMeanValueCheckEnabled)
            {
                if (ResultConfig.MeanValueMin >= ResultConfig.MeanValueMax)
                    result.AddError($"MeanValueMin({ResultConfig.MeanValueMin}) must be less than MeanValueMax({ResultConfig.MeanValueMax})");
            }

            if (ResultConfig.IsStdDevCheckEnabled)
            {
                if (ResultConfig.StdDevMin >= ResultConfig.StdDevMax)
                    result.AddError($"StdDevMin({ResultConfig.StdDevMin}) must be less than StdDevMax({ResultConfig.StdDevMax})");
            }

            if (ResultConfig.IsPSNRCheckEnabled)
            {
                if (ResultConfig.PSNRMin >= ResultConfig.PSNRMax)
                    result.AddError($"PSNRMin({ResultConfig.PSNRMin}) must be less than PSNRMax({ResultConfig.PSNRMax})");
            }
        }

        /// <summary>
        /// 获取滤波类型选项
        /// </summary>
        public static IReadOnlyList<string> GetFilterTypeOptions()
        {
            return new List<string>
            {
                "Gaussian",
                "Median",
                "Mean",
                "Bilateral",
                "Guided"
            };
        }
    }
}