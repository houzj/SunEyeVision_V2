using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ShapeMatch.Models;

namespace SunEyeVision.Tool.ShapeMatch
{
    /// <summary>
    /// 轮廓匹配工具参数
    /// </summary>
    /// <remarks>
    /// 继承 RegionParameters，自带区域管理（搜索区域 = 有效区 + 排除区）。
    /// 使用 ParamValue 包装器统一管理参数值和绑定配置。
    /// 
    /// 参数设计（对齐 UI v1.4 - 模板下拉选择器方案）：
    /// - 全局参数：下方所有 ParamValue 属性
    /// - 每模型覆盖：PerModelOverrides 列表
    /// </remarks>
    [JsonDerivedType(typeof(ShapeMatchParameters), "ShapeMatch")]
    public class ShapeMatchParameters : RegionParameters
    {
        #region 匹配参数

        /// <summary>金字塔层数（0=自动）</summary>
        public ParamValue<int> PyramidLevels { get; set; } =
            ParamValue<int>.CreateConstant(0, "PyramidLevels");

        /// <summary>速度尺度（1~10，值越大越快但精度越低，0=自动）</summary>
        public ParamValue<int> SpeedScale { get; set; } =
            ParamValue<int>.CreateConstant(0, "SpeedScale");

        /// <summary>最小匹配分数（0~1）</summary>
        public ParamValue<double> MinMatchScore { get; set; } =
            ParamValue<double>.CreateConstant(0.5, "MinMatchScore");

        /// <summary>最大匹配数量</summary>
        public ParamValue<int> MaxMatchCount { get; set; } =
            ParamValue<int>.CreateConstant(1, "MaxMatchCount");

        /// <summary>匹配超时（毫秒）</summary>
        public ParamValue<int> MatchTimeoutMs { get; set; } =
            ParamValue<int>.CreateConstant(5000, "MatchTimeoutMs");

        /// <summary>角度步长（度）</summary>
        public ParamValue<double> AngleStep { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "AngleStep");

        /// <summary>角度范围最小值（度）</summary>
        public ParamValue<double> AngleMin { get; set; } =
            ParamValue<double>.CreateConstant(-180.0, "AngleMin");

        /// <summary>角度范围最大值（度）</summary>
        public ParamValue<double> AngleMax { get; set; } =
            ParamValue<double>.CreateConstant(180.0, "AngleMax");

        /// <summary>缩放范围最小值</summary>
        public ParamValue<double> ScaleMin { get; set; } =
            ParamValue<double>.CreateConstant(0.8, "ScaleMin");

        /// <summary>缩放范围最大值</summary>
        public ParamValue<double> ScaleMax { get; set; } =
            ParamValue<double>.CreateConstant(1.2, "ScaleMax");

        /// <summary>缩放步长</summary>
        public ParamValue<double> ScaleStep { get; set; } =
            ParamValue<double>.CreateConstant(0.01, "ScaleStep");

        /// <summary>最小间距/NMS（像素）</summary>
        public ParamValue<int> MinMatchDistance { get; set; } =
            ParamValue<int>.CreateConstant(10, "MinMatchDistance");

        #endregion

        #region 亚像素精定位

        /// <summary>是否启用亚像素精定位</summary>
        public ParamValue<bool> EnableSubPixel { get; set; } =
            ParamValue<bool>.CreateConstant(true, "EnableSubPixel");

        /// <summary>亚像素搜索半径（像素）</summary>
        public ParamValue<double> SubPixelRadius { get; set; } =
            ParamValue<double>.CreateConstant(2.0, "SubPixelRadius");

        #endregion

        #region 结果排序

        /// <summary>排序方式</summary>
        public ParamValue<SortMode> SortMode { get; set; } =
            ParamValue<SortMode>.CreateConstant(Models.SortMode.None, "SortMode");

        #endregion

        #region 高级参数

        /// <summary>执行模式（多模板）</summary>
        public ParamValue<MultiTemplateMode> MultiMode { get; set; } =
            ParamValue<MultiTemplateMode>.CreateConstant(MultiTemplateMode.ParallelShared, "MultiMode");

        /// <summary>最大并行度（0=自动/CPU核数）</summary>
        public ParamValue<int> MaxParallelism { get; set; } =
            ParamValue<int>.CreateConstant(0, "MaxParallelism");

        /// <summary>模板目标点数（影响模型精度与速度）</summary>
        public ParamValue<int> TargetPointCount { get; set; } =
            ParamValue<int>.CreateConstant(60, "TargetPointCount");

        #endregion

        #region 模板管理

        /// <summary>模板列表</summary>
        public ObservableCollection<TemplateModel> Templates { get; set; } = new();

        /// <summary>每模型参数覆盖（多模板时覆盖全局参数）</summary>
        public List<PerModelParamOverride> PerModelOverrides { get; set; } = new();

        #endregion

        #region 结果判断配置

        /// <summary>结果判断配置与显示样式</summary>
        public Models.ShapeMatchResultConfig ResultConfig { get; set; } = new();

        #endregion

        #region 位置修正

        private string? _positionCorrectionBindingSource;
        private string? _positionCorrectionBindingDisplayText;

        /// <summary>是否启用位置修正</summary>
        public ParamValue<bool> IsPositionCorrectionEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsPositionCorrectionEnabled");

        /// <summary>位置修正绑定源路径（可序列化）</summary>
        [JsonPropertyOrder(16)]
        [JsonPropertyName("positionCorrectionBindingSource")]
        public string? PositionCorrectionBindingSource
        {
            get => _positionCorrectionBindingSource;
            set => SetProperty(ref _positionCorrectionBindingSource, value);
        }

        /// <summary>位置修正绑定显示文本（可序列化）</summary>
        [JsonPropertyOrder(17)]
        [JsonPropertyName("positionCorrectionBindingDisplayText")]
        public string? PositionCorrectionBindingDisplayText
        {
            get => _positionCorrectionBindingDisplayText;
            set => SetProperty(ref _positionCorrectionBindingDisplayText, value);
        }

        #endregion

        #region 参数验证

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            if (MinMatchScore.Value < 0 || MinMatchScore.Value > 1.0)
                result.AddError($"最小匹配分数必须在 0~1 范围内，当前值: {MinMatchScore.Value}");

            if (MaxMatchCount.Value < 1)
                result.AddError($"最大匹配数量必须大于0，当前值: {MaxMatchCount.Value}");

            if (MatchTimeoutMs.Value <= 0)
                result.AddError($"匹配超时必须大于0，当前值: {MatchTimeoutMs.Value}ms");

            if (SpeedScale.Value < 0 || SpeedScale.Value > 10)
                result.AddError($"速度尺度必须在 0~10 范围内，当前值: {SpeedScale.Value}");

            if (AngleMin.Value >= AngleMax.Value)
                result.AddError($"角度范围无效: 最小值({AngleMin.Value})必须小于最大值({AngleMax.Value})");

            if (ScaleMin.Value >= ScaleMax.Value)
                result.AddError($"缩放范围无效: 最小值({ScaleMin.Value})必须小于最大值({ScaleMax.Value})");

            if (ScaleStep.Value <= 0)
                result.AddError($"缩放步长必须大于0，当前值: {ScaleStep.Value}");

            if (MinMatchDistance.Value < 0)
                result.AddError($"最小间距(NMS)不能为负数，当前值: {MinMatchDistance.Value}");

            if (SubPixelRadius.Value <= 0 && EnableSubPixel.Value)
                result.AddError($"亚像素搜索半径必须大于0，当前值: {SubPixelRadius.Value}");

            if (MaxParallelism.Value < 0)
                result.AddError($"最大并行度不能为负数，当前值: {MaxParallelism.Value}");

            if (TargetPointCount.Value < 10)
                result.AddError($"模板目标点数建议不小于10，当前值: {TargetPointCount.Value}");

            return result;
        }

        #endregion
    }
}
