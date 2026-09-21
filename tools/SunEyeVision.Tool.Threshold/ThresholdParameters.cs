using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.Threshold
{
    /// <summary>
    /// 阈值类型枚举
    /// </summary>
    public enum ThresholdType
    {
        Binary = 0,
        BinaryInv = 1,
        Trunc = 2,
        ToZero = 3,
        ToZeroInv = 4
    }

    /// <summary>
    /// 自适应方法枚举
    /// </summary>
    public enum AdaptiveMethod
    {
        Mean = 0,
        Gaussian = 1
    }

    /// <summary>
    /// 图像阈值化工具参数
    /// </summary>
    /// <remarks>
    /// 参数类继承 RoiParameters，自带区域管理功能。
    /// 使用 纯 CLR 参数，统一管理参数值和绑定配置。
    /// 算法层直接使用 parameters.Threshold，完全无感。
    /// 
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "Threshold"。
    /// System.Text.Json 会自动添加 "$type" 字段并在反序列化时识别。
    /// 
    /// 参数验证流程：
    /// 1. Validate() 方法：完整验证所有约束条件
    /// </remarks>
    [JsonDerivedType(typeof(ThresholdParameters), "Threshold")]
    public class ThresholdParameters : RoiParameters
    {
        #region 常量定义

        /// <summary>
        /// 阈值最小值
        /// </summary>
        public const int ThresholdMin = 0;

        /// <summary>
        /// 阈值最大值
        /// </summary>
        public const int ThresholdMax = 255;

        /// <summary>
        /// 块大小最小值
        /// </summary>
        public const int BlockSizeMin = 3;

        /// <summary>
        /// 块大小最大值
        /// </summary>
        public const int BlockSizeMax = 31;

        #endregion

        #region 基本参数

        /// <summary>
        /// 二值化阈值(0-255)
        /// </summary>
        [DisplayName("阈值")]
        public int Threshold { get; set; } = 128;

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        [DisplayName("最大值")]
        public int MaxValue { get; set; } = 255;

        /// <summary>
        /// 二值化方法
        /// </summary>
        [DisplayName("二值化方法")]
        public ThresholdType Type { get; set; } = ThresholdType.Binary;

        /// <summary>
        /// 是否反转二值化结果
        /// </summary>
        [DisplayName("反转结果")]
        public bool Invert { get; set; } = false;

        #endregion

        #region 高级参数

        /// <summary>
        /// 自适应阈值方法
        /// </summary>
        [DisplayName("自适应方法")]
        public AdaptiveMethod AdaptiveMethod { get; set; } = SunEyeVision.Tool.Threshold.AdaptiveMethod.Mean;

        /// <summary>
        /// 计算阈值的邻域大小(奇数, 3-31)
        /// </summary>
        [DisplayName("块大小")]
        public int BlockSize { get; set; } = 11;

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <remarks>
        /// 验证规则：
        /// 1. 块大小必须为奇数
        /// 2. Threshold 在 [0, 255] 范围内
        /// 3. MaxValue 在 [0, 255] 范围内
        /// 4. BlockSize 在 [3, 31] 范围内
        /// 5. ResultConfig 各项范围值有效（MinValue < MaxValue）
        /// </remarks>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            // 块大小必须为奇数
            if (BlockSize % 2 == 0)
            {
                result.AddError($"块大小必须为奇数，当前值: {BlockSize}");
            }

            // 块大小范围检查
            if (BlockSize < BlockSizeMin || BlockSize > BlockSizeMax)
            {
                result.AddError($"块大小必须在 {BlockSizeMin}-{BlockSizeMax} 范围内，当前值: {BlockSize}");
            }

            // 阈值范围检查
            if (Threshold < ThresholdMin || Threshold > ThresholdMax)
            {
                result.AddError($"阈值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {Threshold}");
            }

            // 最大值范围检查
            if (MaxValue < ThresholdMin || MaxValue > ThresholdMax)
            {
                result.AddError($"最大值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {MaxValue}");
            }

            // 结果配置验证
            ValidateResultConfig(result);

            return result;
        }

        /// <summary>
        /// 验证结果判断配置
        /// </summary>
        private void ValidateResultConfig(ValidationResult result)
        {
            if (ResultPresentation == null)
            {
                result.AddError("结果判断配置不能为空");
                return;
            }

            foreach (var item in ResultPresentation.Judgments)
                if (item.Enabled && item.RuleKind == JudgmentRuleKind.Range && item.Min >= item.Max)
                    result.AddError($"{item.Name}最小值({item.Min})必须小于最大值({item.Max})");
        }

        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "output-not-empty", Name = "输出不为空", RuleKind = JudgmentRuleKind.Boolean, Enabled = false, Order = 10 });
            config.Judgments.Add(new() { Key = "white-ratio", Name = "白色像素比例", Enabled = false, Min = 0, Max = 100, Unit = "%", AllowedMinimum = 0, AllowedMaximum = 100, DecimalPlaces = 1, Order = 20 });
            config.Judgments.Add(new() { Key = "mean", Name = "输出均值", Enabled = false, Min = 0, Max = 255, AllowedMinimum = 0, AllowedMaximum = 255, DecimalPlaces = 1, Order = 30 });
            config.Judgments.Add(new() { Key = "area", Name = "输出面积", Enabled = false, Min = 0, Max = 1000000, Unit = "px²", AllowedMinimum = 0, AllowedMaximum = 1000000, DecimalPlaces = 0, Order = 40 });
            config.Judgments.Add(new() { Key = "centroid-x", Name = "质心X", Enabled = false, Min = 0, Max = 10000, AllowedMinimum = 0, AllowedMaximum = 10000, DecimalPlaces = 1, Order = 50 });
            config.Judgments.Add(new() { Key = "centroid-y", Name = "质心Y", Enabled = false, Min = 0, Max = 10000, AllowedMinimum = 0, AllowedMaximum = 10000, DecimalPlaces = 1, Order = 60 });
            return config;
        }

        /// <summary>
        /// 获取阈值类型选项
        /// </summary>
        public static IReadOnlyList<string> GetThresholdTypeOptions()
        {
            return new List<string> { "Binary", "BinaryInv", "Trunc", "ToZero", "ToZeroInv" };
        }

        /// <summary>
        /// 获取自适应方法选项
        /// </summary>
        public static IReadOnlyList<string> GetAdaptiveMethodOptions()
        {
            return new List<string> { "Mean", "Gaussian" };
        }
    }
}
