using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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
    /// 图像预处理滤波类型
    /// </summary>
    public enum PreprocessFilterType
    {
        Gaussian = 0,
        Median = 1,
        Smooth = 2,
        Emphasize = 3
    }

    /// <summary>
    /// 形态学操作类型
    /// </summary>
    public enum MorphOperation
    {
        Erode = 0,
        Dilate = 1,
        Open = 2,
        Close = 3,
        Gradient = 4,
        TopHat = 5,
        BlackHat = 6
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

        #region 图像预处理参数

        /// <summary>是否启用图像预处理</summary>
        public ParamValue<bool> IsImagePreprocessingEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsImagePreprocessingEnabled");

        /// <summary>预处理滤波类型</summary>
        public ParamValue<PreprocessFilterType> PreprocessFilterType { get; set; } =
            ParamValue<PreprocessFilterType>.CreateConstant((PreprocessFilterType)0, "PreprocessFilterType");

        /// <summary>内核大小（奇数）</summary>
        public ParamValue<int> PreprocessKernelSize { get; set; } =
            ParamValue<int>.CreateConstant(3, "PreprocessKernelSize");

        /// <summary>高斯滤波 Sigma</summary>
        public ParamValue<double> PreprocessSigma { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "PreprocessSigma");

        /// <summary>锐化强度（仅 Emphasize 类型有效）</summary>
        public ParamValue<double> PreprocessEmphasizeFactor { get; set; } =
            ParamValue<double>.CreateConstant(1.5, "PreprocessEmphasizeFactor");

        [JsonIgnore] public bool IsGaussianMethod => PreprocessFilterType.Value == (PreprocessFilterType)0;
        [JsonIgnore] public bool IsMedianMethod => PreprocessFilterType.Value == (PreprocessFilterType)1;
        [JsonIgnore] public bool IsSmoothMethod => PreprocessFilterType.Value == (PreprocessFilterType)2;
        [JsonIgnore] public bool IsEmphasizeMethod => PreprocessFilterType.Value == (PreprocessFilterType)3;

        #endregion

        #region 预处理参数（阈值化）

        /// <summary>
        /// 二值化阈值(0-255)
        /// </summary>
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 二值化方法（仅支持 Binary/BinaryInv）
        /// </summary>
        public ParamValue<ThresholdType> Type { get; set; } =
            ParamValue<ThresholdType>.CreateConstant(ThresholdType.Binary, "Type");

        /// <summary>
        /// 是否反转二值化结果
        /// </summary>
        public ParamValue<bool> Invert { get; set; } =
            ParamValue<bool>.CreateConstant(false, "Invert");

        #endregion

        #region Blob筛选参数

        /// <summary>
        /// 最小面积（像素数）
        /// </summary>
        public ParamValue<int> MinArea { get; set; } =
            ParamValue<int>.CreateConstant(0, "MinArea");

        /// <summary>
        /// 最大面积（像素数）
        /// </summary>
        public ParamValue<int> MaxArea { get; set; } =
            ParamValue<int>.CreateConstant(999999, "MaxArea");

        /// <summary>
        /// 最小圆度(0~1)
        /// </summary>
        public ParamValue<double> MinCircularity { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "MinCircularity");

        /// <summary>
        /// 最大圆度(0~1)
        /// </summary>
        public ParamValue<double> MaxCircularity { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "MaxCircularity");

        /// <summary>
        /// 最小矩形度(0~1)
        /// </summary>
        public ParamValue<double> MinRectangularity { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "MinRectangularity");

        /// <summary>
        /// 最大矩形度(0~1)
        /// </summary>
        public ParamValue<double> MaxRectangularity { get; set; } =
            ParamValue<double>.CreateConstant(1.0, "MaxRectangularity");

        #endregion

        #region 形态学预处理参数

        /// <summary>是否启用形态学预处理</summary>
        public ParamValue<bool> IsMorphologyEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsMorphologyEnabled");

        /// <summary>形态学操作类型</summary>
        public ParamValue<MorphOperation> MorphOperation { get; set; } =
            ParamValue<MorphOperation>.CreateConstant((MorphOperation)2, "MorphOperation");

        /// <summary>结构元素大小（奇数）</summary>
        public ParamValue<int> MorphKernelSize { get; set; } =
            ParamValue<int>.CreateConstant(3, "MorphKernelSize");

        /// <summary>迭代次数</summary>
        public ParamValue<int> MorphIterations { get; set; } =
            ParamValue<int>.CreateConstant(1, "MorphIterations");

        #endregion

        #region 显示样式

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public BlobAnalysisResultConfig ResultConfig { get; set; } = new();

        /// <summary>外接矩形样式</summary>
        public GraphicsStyle BlobsStyle { get; set; } = new() { Name = "Blobs", LineWidth = 2 };

        /// <summary>质心标记样式</summary>
        public GraphicsStyle CentroidsStyle { get; set; } = new() { Name = "Centroids", LineWidth = 2, CrossSize = 8 };

        /// <summary>轮廓样式</summary>
        public GraphicsStyle ContoursStyle { get; set; } = new() { Name = "Contours", LineWidth = 1 };

        /// <summary>面积列表样式</summary>
        public TextStyle AreaDisplayStyle { get; set; } = new() { Name = "AreaDisplay", FontSize = 9.0, PositionX = 10, PositionY = 50 };

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
        /// 位置修正绑定源路径（可序列化）
        /// </summary>
        /// <remarks>
        /// 格式：节点ID.属性名，如 "templateMatch1.PositionOffset"
        /// 用户通过 ParamBinding 控件选择上游节点的位置修正输出自动设置
        /// </remarks>
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
        /// <remarks>
        /// 格式：节点名.显示名，如 "模板匹配1.位置偏移"
        /// </remarks>
        [JsonPropertyOrder(17)]
        [JsonPropertyName("positionCorrectionBindingDisplayText")]
        public string? PositionCorrectionBindingDisplayText
        {
            get => _positionCorrectionBindingDisplayText;
            set => SetProperty(ref _positionCorrectionBindingDisplayText, value);
        }

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

            // 图像预处理参数验证
            if (IsImagePreprocessingEnabled.Value)
            {
                if (PreprocessKernelSize.Value < 1 || PreprocessKernelSize.Value > 31)
                    result.AddError($"预处理内核大小必须在 1-31 范围内，当前值: {PreprocessKernelSize.Value}");
                if (PreprocessKernelSize.Value % 2 == 0)
                    result.AddError($"预处理内核大小必须为奇数，当前值: {PreprocessKernelSize.Value}");
            }

            // 形态学预处理参数验证
            if (IsMorphologyEnabled.Value)
            {
                if (MorphKernelSize.Value < 1 || MorphKernelSize.Value > 51)
                    result.AddError($"形态学内核大小必须在 1-51 范围内，当前值: {MorphKernelSize.Value}");
                if (MorphKernelSize.Value % 2 == 0)
                    result.AddError($"形态学内核大小必须为奇数，当前值: {MorphKernelSize.Value}");
                if (MorphIterations.Value < 1 || MorphIterations.Value > 20)
                    result.AddError($"形态学迭代次数必须在 1-20 范围内，当前值: {MorphIterations.Value}");
            }

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

            // 平均面积范围验证
            if (ResultConfig.IsAverageAreaCheckEnabled)
            {
                if (ResultConfig.AverageAreaMin >= ResultConfig.AverageAreaMax)
                {
                    result.AddError($"平均面积最小值({ResultConfig.AverageAreaMin})必须小于最大值({ResultConfig.AverageAreaMax})");
                }
            }

            // 最大面积范围验证
            if (ResultConfig.IsMaxAreaCheckEnabled)
            {
                if (ResultConfig.MaxAreaMin >= ResultConfig.MaxAreaMax)
                {
                    result.AddError($"最大面积最小值({ResultConfig.MaxAreaMin})必须小于最大值({ResultConfig.MaxAreaMax})");
                }
            }

            // 最小面积范围验证
            if (ResultConfig.IsMinAreaCheckEnabled)
            {
                if (ResultConfig.MinAreaMin >= ResultConfig.MinAreaMax)
                {
                    result.AddError($"最小面积最小值({ResultConfig.MinAreaMin})必须小于最大值({ResultConfig.MinAreaMax})");
                }
            }

            // 平均圆度范围验证
            if (ResultConfig.IsAverageCircularityCheckEnabled)
            {
                if (ResultConfig.AverageCircularityMin < 0 || ResultConfig.AverageCircularityMin > 1.0)
                {
                    result.AddError($"平均圆度最小值({ResultConfig.AverageCircularityMin})必须在 0-1 范围内");
                }
                if (ResultConfig.AverageCircularityMax < 0 || ResultConfig.AverageCircularityMax > 1.0)
                {
                    result.AddError($"平均圆度最大值({ResultConfig.AverageCircularityMax})必须在 0-1 范围内");
                }
                if (ResultConfig.AverageCircularityMin >= ResultConfig.AverageCircularityMax)
                {
                    result.AddError($"平均圆度最小值({ResultConfig.AverageCircularityMin})必须小于最大值({ResultConfig.AverageCircularityMax})");
                }
            }

            // 平均矩形度范围验证
            if (ResultConfig.IsAverageRectangularityCheckEnabled)
            {
                if (ResultConfig.AverageRectangularityMin < 0 || ResultConfig.AverageRectangularityMin > 1.0)
                {
                    result.AddError($"平均矩形度最小值({ResultConfig.AverageRectangularityMin})必须在 0-1 范围内");
                }
                if (ResultConfig.AverageRectangularityMax < 0 || ResultConfig.AverageRectangularityMax > 1.0)
                {
                    result.AddError($"平均矩形度最大值({ResultConfig.AverageRectangularityMax})必须在 0-1 范围内");
                }
                if (ResultConfig.AverageRectangularityMin >= ResultConfig.AverageRectangularityMax)
                {
                    result.AddError($"平均矩形度最小值({ResultConfig.AverageRectangularityMin})必须小于最大值({ResultConfig.AverageRectangularityMax})");
                }
            }

            // ===== Per-Blob判断验证 =====

            // 每个Blob面积范围验证
            if (ResultConfig.IsPerBlobAreaCheckEnabled)
            {
                if (ResultConfig.PerBlobAreaMin >= ResultConfig.PerBlobAreaMax)
                {
                    result.AddError($"每个Blob面积最小值({ResultConfig.PerBlobAreaMin})必须小于最大值({ResultConfig.PerBlobAreaMax})");
                }
            }

            // 每个Blob圆度范围验证
            if (ResultConfig.IsPerBlobCircularityCheckEnabled)
            {
                if (ResultConfig.PerBlobCircularityMin < 0 || ResultConfig.PerBlobCircularityMin > 1.0)
                {
                    result.AddError($"每个Blob圆度最小值({ResultConfig.PerBlobCircularityMin})必须在 0-1 范围内");
                }
                if (ResultConfig.PerBlobCircularityMax < 0 || ResultConfig.PerBlobCircularityMax > 1.0)
                {
                    result.AddError($"每个Blob圆度最大值({ResultConfig.PerBlobCircularityMax})必须在 0-1 范围内");
                }
                if (ResultConfig.PerBlobCircularityMin >= ResultConfig.PerBlobCircularityMax)
                {
                    result.AddError($"每个Blob圆度最小值({ResultConfig.PerBlobCircularityMin})必须小于最大值({ResultConfig.PerBlobCircularityMax})");
                }
            }

            // 每个Blob矩形度范围验证
            if (ResultConfig.IsPerBlobRectangularityCheckEnabled)
            {
                if (ResultConfig.PerBlobRectangularityMin < 0 || ResultConfig.PerBlobRectangularityMin > 1.0)
                {
                    result.AddError($"每个Blob矩形度最小值({ResultConfig.PerBlobRectangularityMin})必须在 0-1 范围内");
                }
                if (ResultConfig.PerBlobRectangularityMax < 0 || ResultConfig.PerBlobRectangularityMax > 1.0)
                {
                    result.AddError($"每个Blob矩形度最大值({ResultConfig.PerBlobRectangularityMax})必须在 0-1 范围内");
                }
                if (ResultConfig.PerBlobRectangularityMin >= ResultConfig.PerBlobRectangularityMax)
                {
                    result.AddError($"每个Blob矩形度最小值({ResultConfig.PerBlobRectangularityMin})必须小于最大值({ResultConfig.PerBlobRectangularityMax})");
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
    }
}
