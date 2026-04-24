using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.BlobAnalysis.Models;

namespace SunEyeVision.Tool.BlobAnalysis
{
    /// <summary>
    /// 阈值类型枚举（Blob分析预处理用）
    /// </summary>
    public enum ThresholdType
    {
        Binary = 0,
        BinaryInv = 1
    }

    /// <summary>
    /// 连通性类型枚举
    /// </summary>
    public enum ConnectivityType
    {
        Connect4 = 0,
        Connect8 = 1
    }

    /// <summary>
    /// Blob分析工具参数
    /// </summary>
    /// <remarks>
    /// 参数类继承 RegionParameters，自带区域管理功能。
    /// 使用 ParamValue 包装器，统一管理参数值和绑定配置。
    /// 
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "BlobAnalysis"。
    /// </remarks>
    [JsonDerivedType(typeof(BlobAnalysisParameters), "BlobAnalysis")]
    public class BlobAnalysisParameters : RegionParameters
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

        #endregion

        #region 预处理参数（阈值化）

        /// <summary>
        /// 二值化阈值(0-255)
        /// </summary>
        [JsonPropertyOrder(0)]
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        [JsonPropertyOrder(1)]
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 二值化方法（仅支持 Binary/BinaryInv）
        /// </summary>
        [JsonPropertyOrder(2)]
        public ParamValue<ThresholdType> Type { get; set; } =
            ParamValue<ThresholdType>.CreateConstant(ThresholdType.Binary, "Type");

        /// <summary>
        /// 是否反转二值化结果
        /// </summary>
        [JsonPropertyOrder(3)]
        public ParamValue<bool> Invert { get; set; } =
            ParamValue<bool>.CreateConstant(false, "Invert");

        #endregion

        #region Blob筛选参数

        /// <summary>
        /// 最小面积（像素数）
        /// </summary>
        [JsonPropertyOrder(10)]
        public ParamValue<int> MinArea { get; set; } =
            ParamValue<int>.CreateConstant(0, "MinArea");

        /// <summary>
        /// 最大面积（像素数）
        /// </summary>
        [JsonPropertyOrder(11)]
        public ParamValue<int> MaxArea { get; set; } =
            ParamValue<int>.CreateConstant(999999, "MaxArea");

        /// <summary>
        /// 最小圆度(0~1)
        /// </summary>
        [JsonPropertyOrder(12)]
        public ParamValue<double> MinCircularity { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "MinCircularity");

        /// <summary>
        /// 最大圆度(0~1)
        /// </summary>
        [JsonPropertyOrder(13)]
        public ParamValue<double> MaxCircularity { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "MaxCircularity");

        /// <summary>
        /// 最小矩形度(0~1)
        /// </summary>
        [JsonPropertyOrder(14)]
        public ParamValue<double> MinRectangularity { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "MinRectangularity");

        /// <summary>
        /// 最大矩形度(0~1)
        /// </summary>
        [JsonPropertyOrder(15)]
        public ParamValue<double> MaxRectangularity { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "MaxRectangularity");

        /// <summary>
        /// 连通性类型(4连通/8连通)
        /// </summary>
        [JsonPropertyOrder(16)]
        public ParamValue<ConnectivityType> Connectivity { get; set; } =
            ParamValue<ConnectivityType>.CreateConstant(ConnectivityType.Connect8, "Connectivity");

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        [JsonPropertyOrder(20)]
        [JsonPropertyName("resultConfig")]
        public BlobAnalysisResultConfig ResultConfig { get; set; } = new();

        /// <summary>
        /// 图像显示配置
        /// </summary>
        [JsonPropertyOrder(21)]
        [JsonPropertyName("displayConfig")]
        public BlobAnalysisDisplayConfig DisplayConfig { get; set; } = new();

        /// <summary>
        /// 文本显示配置
        /// </summary>
        [JsonPropertyOrder(22)]
        [JsonPropertyName("textConfig")]
        public BlobAnalysisTextConfig TextConfig { get; set; } = new();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

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

            // Blob筛选参数范围检查
            if (MinArea.Value < 0)
            {
                result.AddError($"最小面积不能为负数，当前值: {MinArea.Value}");
            }

            if (MinArea.Value > MaxArea.Value)
            {
                result.AddError($"最小面积({MinArea.Value})不能大于最大面积({MaxArea.Value})");
            }

            if (MinCircularity.Value < 0 || MinCircularity.Value > 1.0)
            {
                result.AddError($"最小圆度必须在 0-1 范围内，当前值: {MinCircularity.Value}");
            }

            if (MaxCircularity.Value < 0 || MaxCircularity.Value > 1.0)
            {
                result.AddError($"最大圆度必须在 0-1 范围内，当前值: {MaxCircularity.Value}");
            }

            if (MinCircularity.Value > MaxCircularity.Value)
            {
                result.AddError($"最小圆度({MinCircularity.Value})不能大于最大圆度({MaxCircularity.Value})");
            }

            if (MinRectangularity.Value < 0 || MinRectangularity.Value > 1.0)
            {
                result.AddError($"最小矩形度必须在 0-1 范围内，当前值: {MinRectangularity.Value}");
            }

            if (MaxRectangularity.Value < 0 || MaxRectangularity.Value > 1.0)
            {
                result.AddError($"最大矩形度必须在 0-1 范围内，当前值: {MaxRectangularity.Value}");
            }

            if (MinRectangularity.Value > MaxRectangularity.Value)
            {
                result.AddError($"最小矩形度({MinRectangularity.Value})不能大于最大矩形度({MaxRectangularity.Value})");
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

            // Blob数量范围验证
            if (ResultConfig.IsBlobCountCheckEnabled)
            {
                if (ResultConfig.BlobCountMin >= ResultConfig.BlobCountMax)
                {
                    result.AddError($"Blob数量最小值({ResultConfig.BlobCountMin})必须小于最大值({ResultConfig.BlobCountMax})");
                }
            }

            // 总面积范围验证
            if (ResultConfig.IsTotalAreaCheckEnabled)
            {
                if (ResultConfig.TotalAreaMin >= ResultConfig.TotalAreaMax)
                {
                    result.AddError($"总面积最小值({ResultConfig.TotalAreaMin})必须小于最大值({ResultConfig.TotalAreaMax})");
                }
            }
        }

        /// <summary>
        /// 获取阈值类型选项
        /// </summary>
        public static IReadOnlyList<string> GetThresholdTypeOptions()
        {
            return new List<string> { "Binary", "BinaryInv" };
        }

        /// <summary>
        /// 获取连通性类型选项
        /// </summary>
        public static IReadOnlyList<string> GetConnectivityTypeOptions()
        {
            return new List<string> { "Connect4", "Connect8" };
        }
    }
}
