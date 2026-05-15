using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.CvEngine;
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

        /// <summary>最小匹配分数（0~1）</summary>
        public ParamValue<double> MinMatchScore { get; set; } =
            ParamValue<double>.CreateConstant(0.5, "MinMatchScore");

        /// <summary>最大匹配数量</summary>
        public ParamValue<int> MaxMatchCount { get; set; } =
            ParamValue<int>.CreateConstant(1, "MaxMatchCount");

        /// <summary>匹配超时（毫秒）</summary>
        public ParamValue<int> MatchTimeoutMs { get; set; } =
            ParamValue<int>.CreateConstant(30000, "MatchTimeoutMs");

        /// <summary>角度步长（度）</summary>
        public ParamValue<double> AngleStep { get; set; } =
            ParamValue<double>.CreateConstant(2.0, "AngleStep");

        /// <summary>角度范围最小值（度）</summary>
        public ParamValue<double> AngleMin { get; set; } =
            ParamValue<double>.CreateConstant(-45.0, "AngleMin");

        /// <summary>角度范围最大值（度）</summary>
        public ParamValue<double> AngleMax { get; set; } =
            ParamValue<double>.CreateConstant(45.0, "AngleMax");

        /// <summary>缩放范围最小值</summary>
        public ParamValue<double> ScaleMin { get; set; } =
            ParamValue<double>.CreateConstant(0.8, "ScaleMin");

        /// <summary>缩放范围最大值</summary>
        public ParamValue<double> ScaleMax { get; set; } =
            ParamValue<double>.CreateConstant(1.2, "ScaleMax");

        /// <summary>缩放步长</summary>
        public ParamValue<double> ScaleStep { get; set; } =
            ParamValue<double>.CreateConstant(0.02, "ScaleStep");

        /// <summary>最大返回匹配数（Halcon NumMatches）</summary>
        public ParamValue<int> NumMatches { get; set; } =
            ParamValue<int>.CreateConstant(1, "NumMatches");

        /// <summary>NMS 最大重叠率 0~1（Halcon MaxOverlap）</summary>
        public ParamValue<double> MaxOverlap { get; set; } =
            ParamValue<double>.CreateConstant(0.5, "MaxOverlap");

        /// <summary>贪婪度 0~1（Halcon Greediness）</summary>
        public ParamValue<double> Greediness { get; set; } =
            ParamValue<double>.CreateConstant(0.9, "Greediness");

        #endregion

        #region 最小梯度阈值（响应图噪声过滤）

        /// <summary>最小梯度阈值模式</summary>
        public ParamValue<MinContrastMode> MinContrastMode { get; set; } =
            ParamValue<MinContrastMode>.CreateConstant(
                SunEyeVision.CvEngine.MinContrastMode.Auto, "MinContrastMode");

        /// <summary>手动最小对比度值（仅 Manual 模式有效）</summary>
        public ParamValue<float> MinContrast { get; set; } =
            ParamValue<float>.CreateConstant(10f, "MinContrast");

        #endregion

        #region 亚像素精定位

        /// <summary>是否启用亚像素精定位</summary>
        public ParamValue<bool> EnableSubPixel { get; set; } =
            ParamValue<bool>.CreateConstant(true, "EnableSubPixel");

        // 亚像素搜索半径固定为 2px，不再暴露

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

            if (AngleMin.Value > AngleMax.Value)
                result.AddError($"角度范围无效: 最小值({AngleMin.Value})不能大于最大值({AngleMax.Value})");

            if (ScaleMin.Value > ScaleMax.Value)
                result.AddError($"缩放范围无效: 最小值({ScaleMin.Value})不能大于最大值({ScaleMax.Value})");

            if (ScaleStep.Value <= 0)
                result.AddError($"缩放步长必须大于0，当前值: {ScaleStep.Value}");

            if (NumMatches.Value < 1)
                result.AddError($"NumMatches({NumMatches.Value}) 必须 >= 1");

            if (MaxOverlap.Value < 0 || MaxOverlap.Value > 1)
                result.AddError($"MaxOverlap({MaxOverlap.Value}) 必须在 0~1 范围内");

            if (Greediness.Value < 0 || Greediness.Value > 1)
                result.AddError($"Greediness({Greediness.Value}) 必须在 0~1 范围内");

            if (MaxParallelism.Value < 0)
                result.AddError($"最大并行度不能为负数，当前值: {MaxParallelism.Value}");

            if (MinContrastMode.Value == SunEyeVision.CvEngine.MinContrastMode.Manual && MinContrast.Value <= 0)
                result.AddError($"手动最小对比度必须大于0，当前值: {MinContrast.Value}");

            return result;
        }

        #endregion
    }
}
