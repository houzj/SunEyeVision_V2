using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.Threshold.Models;

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
    /// 参数类继承 RegionParameters，自带区域管理功能。
    /// 使用 ParamValue 包装器，统一管理参数值和绑定配置。
    /// 算法层直接使用 parameters.Threshold.Value，完全无感。
    /// 
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "Threshold"。
    /// System.Text.Json 会自动添加 "$type" 字段并在反序列化时识别。
    /// 
    /// 参数验证流程：
    /// 1. Validate() 方法：完整验证所有约束条件
    /// </remarks>
    [JsonDerivedType(typeof(ThresholdParameters), "Threshold")]
    public class ThresholdParameters : RegionParameters
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
        /// <remarks>
        /// ParamValue 统一管理值和绑定配置。
        /// 算法层使用：parameters.Threshold.Value
        /// 框架层使用：parameters.Threshold.BindingConfig
        /// </remarks>
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 二值化方法
        /// </summary>
        public ParamValue<ThresholdType> Type { get; set; } =
            ParamValue<ThresholdType>.CreateConstant(ThresholdType.Binary, "Type");

        /// <summary>
        /// 是否反转二值化结果
        /// </summary>
        public ParamValue<bool> Invert { get; set; } =
            ParamValue<bool>.CreateConstant(false, "Invert");

        #endregion

        #region 高级参数

        /// <summary>
        /// 自适应阈值方法
        /// </summary>
        public ParamValue<AdaptiveMethod> AdaptiveMethod { get; set; } =
            ParamValue<AdaptiveMethod>.CreateConstant(SunEyeVision.Tool.Threshold.AdaptiveMethod.Mean, "AdaptiveMethod");

        /// <summary>
        /// 计算阈值的邻域大小(奇数, 3-31)
        /// </summary>
        public ParamValue<int> BlockSize { get; set; } =
            ParamValue<int>.CreateConstant(11, "BlockSize");

        #endregion

        #region 参数绑定

        /// <summary>
        /// 输入图像源（参数绑定）
        /// </summary>
        /// <remarks>
        /// 用于参数面板和调试窗口选择图像源。
        /// 支持从上游节点绑定图像输出。
        /// 
        /// 注意：ImageSource 保持为 AvailableDataSource，不是 ParamValue，
        /// 因为它是特殊的数据源绑定，不需要统一管理。
        /// </remarks>
        [JsonIgnore]
        public AvailableDataSource? ImageSource
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value, "输入图像");
        }

        private AvailableDataSource? _imageSource;

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        [JsonPropertyName("resultConfig")]
        public ThresholdResultConfig ResultConfig { get; set; } = new();

        /// <summary>
        /// 图像显示配置
        /// </summary>
        [JsonPropertyName("displayConfig")]
        public ThresholdDisplayConfig DisplayConfig { get; set; } = new();

        /// <summary>
        /// 文本显示配置
        /// </summary>
        [JsonPropertyName("textConfig")]
        public ThresholdTextConfig TextConfig { get; set; } = new();

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
            if (BlockSize.Value % 2 == 0)
            {
                result.AddError($"块大小必须为奇数，当前值: {BlockSize.Value}");
            }

            // 块大小范围检查
            if (BlockSize.Value < BlockSizeMin || BlockSize.Value > BlockSizeMax)
            {
                result.AddError($"块大小必须在 {BlockSizeMin}-{BlockSizeMax} 范围内，当前值: {BlockSize.Value}");
            }

            // 阈值范围检查
            if (Threshold.Value < ThresholdMin || Threshold.Value > ThresholdMax)
            {
                result.AddError($"阈值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {Threshold.Value}");
            }

            // 最大值范围检查
            if (MaxValue.Value < ThresholdMin || MaxValue.Value > ThresholdMax)
            {
                result.AddError($"最大值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {MaxValue.Value}");
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
            if (ResultConfig == null)
            {
                result.AddError("结果判断配置不能为空");
                return;
            }

            // 白色像素比例范围验证
            if (ResultConfig.IsWhitePixelRatioCheckEnabled)
            {
                if (ResultConfig.WhitePixelRatioMin >= ResultConfig.WhitePixelRatioMax)
                {
                    result.AddError($"白色像素比例最小值({ResultConfig.WhitePixelRatioMin})必须小于最大值({ResultConfig.WhitePixelRatioMax})");
                }
            }

            // 输出均值范围验证
            if (ResultConfig.IsMeanCheckEnabled)
            {
                if (ResultConfig.MeanMin >= ResultConfig.MeanMax)
                {
                    result.AddError($"输出均值最小值({ResultConfig.MeanMin})必须小于最大值({ResultConfig.MeanMax})");
                }
            }

            // 输出面积范围验证
            if (ResultConfig.IsAreaCheckEnabled)
            {
                if (ResultConfig.AreaMin >= ResultConfig.AreaMax)
                {
                    result.AddError($"输出面积最小值({ResultConfig.AreaMin})必须小于最大值({ResultConfig.AreaMax})");
                }
            }

            // 质心X范围验证
            if (ResultConfig.IsCentroidXCheckEnabled)
            {
                if (ResultConfig.CentroidXMin >= ResultConfig.CentroidXMax)
                {
                    result.AddError($"质心X最小值({ResultConfig.CentroidXMin})必须小于最大值({ResultConfig.CentroidXMax})");
                }
            }

            // 质心Y范围验证
            if (ResultConfig.IsCentroidYCheckEnabled)
            {
                if (ResultConfig.CentroidYMin >= ResultConfig.CentroidYMax)
                {
                    result.AddError($"质心Y最小值({ResultConfig.CentroidYMin})必须小于最大值({ResultConfig.CentroidYMax})");
                }
            }
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
