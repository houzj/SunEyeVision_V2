using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageFilter.Models;

namespace SunEyeVision.Tool.ImageFilter
{
    /// <summary>
    /// 滤波类型枚举
    /// </summary>
    public enum FilterType
    {
        Blur = 0,
        Gaussian = 1,
        MedianBlur = 2,
        Bilateral = 3,
        Filter2D = 4
    }

    /// <summary>
    /// 边界处理类型枚举
    /// </summary>
    public enum BorderType
    {
        Reflect = 0,
        Replicate = 1,
        Constant = 2,
        Reflect101 = 3,
        Wrap = 4,
        Isolated = 5
    }

    /// <summary>
    /// 图像滤波工具参数
    /// </summary>
    /// <remarks>
    /// 参数类继承 RegionParameters，自带区域管理功能。
    /// 使用 ParamValue 包装器，统一管理参数值和绑定配置。
    ///
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "ImageFilter"。
    /// </remarks>
    [JsonDerivedType(typeof(ImageFilterParameters), "ImageFilter")]
    public class ImageFilterParameters : RegionParameters
    {
        /// <summary>
        /// 构造函数 - 注册 FilterTypeValue 变更通知
        /// </summary>
        public ImageFilterParameters()
        {
            _filterTypeValue.PropertyChanged += OnFilterTypeValueChanged;
        }

        /// <summary>
        /// 滤波类型变化时更新所有方法分组的可见性
        /// </summary>
        private void OnFilterTypeValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParamValue<int>.Value))
            {
                OnPropertyChanged(nameof(IsBlurMethod));
                OnPropertyChanged(nameof(IsGaussianMethod));
                OnPropertyChanged(nameof(IsMedianBlurMethod));
                OnPropertyChanged(nameof(IsBilateralMethod));
                OnPropertyChanged(nameof(IsFilter2DMethod));
            }
        }

        #region 常量定义

        /// <summary>内核宽度最小值</summary>
        public const int KernelMin = 1;

        /// <summary>内核宽度最大值</summary>
        public const int KernelMax = 31;

        #endregion

        #region 运行参数

        /// <summary>
        /// 滤波类型（字段后备属性，反序列化替换实例时自动恢复事件订阅）
        /// </summary>
        private ParamValue<FilterType> _filterTypeValue = ParamValue<FilterType>.CreateConstant(FilterType.Gaussian, "FilterType");

        public ParamValue<FilterType> FilterTypeValue
        {
            get => _filterTypeValue;
            set
            {
                if (!ReferenceEquals(_filterTypeValue, value))
                {
                    if (_filterTypeValue != null)
                        _filterTypeValue.PropertyChanged -= OnFilterTypeValueChanged;
                    _filterTypeValue = value;
                    if (_filterTypeValue != null)
                        _filterTypeValue.PropertyChanged += OnFilterTypeValueChanged;
                    OnPropertyChanged();
                    // 实例被替换时也刷新所有可见性
                    OnPropertyChanged(nameof(IsBlurMethod));
                    OnPropertyChanged(nameof(IsGaussianMethod));
                    OnPropertyChanged(nameof(IsMedianBlurMethod));
                    OnPropertyChanged(nameof(IsBilateralMethod));
                    OnPropertyChanged(nameof(IsFilter2DMethod));
                }
            }
        }

        #region 方法可见性属性（运行时计算，用于UI分组显示）

        [JsonIgnore]
        public bool IsBlurMethod => FilterTypeValue.Value == FilterType.Blur;

        [JsonIgnore]
        public bool IsGaussianMethod => FilterTypeValue.Value == FilterType.Gaussian;

        [JsonIgnore]
        public bool IsMedianBlurMethod => FilterTypeValue.Value == FilterType.MedianBlur;

        [JsonIgnore]
        public bool IsBilateralMethod => FilterTypeValue.Value == FilterType.Bilateral;

        [JsonIgnore]
        public bool IsFilter2DMethod => FilterTypeValue.Value == FilterType.Filter2D;

        #endregion

        /// <summary>
        /// 内核宽度
        /// </summary>
        public ParamValue<int> KernelWidth { get; set; } =
            ParamValue<int>.CreateConstant(3, "KernelWidth");

        /// <summary>
        /// 内核高度
        /// </summary>
        public ParamValue<int> KernelHeight { get; set; } =
            ParamValue<int>.CreateConstant(3, "KernelHeight");

        /// <summary>
        /// 高斯滤波 SigmaX
        /// </summary>
        public ParamValue<double> SigmaX { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "SigmaX");

        /// <summary>
        /// 高斯滤波 SigmaY
        /// </summary>
        public ParamValue<double> SigmaY { get; set; } =
            ParamValue<double>.CreateConstant(0.0, "SigmaY");

        /// <summary>
        /// 双边滤波颜色空间 Sigma
        /// </summary>
        public ParamValue<double> SigmaColor { get; set; } =
            ParamValue<double>.CreateConstant(10.0, "SigmaColor");

        /// <summary>
        /// 双边滤波坐标空间 Sigma
        /// </summary>
        public ParamValue<double> SigmaSpace { get; set; } =
            ParamValue<double>.CreateConstant(10.0, "SigmaSpace");

        /// <summary>
        /// 边界处理类型
        /// </summary>
        public ParamValue<ImageFilter.BorderType> BorderTypeValue { get; set; } =
            ParamValue<ImageFilter.BorderType>.CreateConstant(ImageFilter.BorderType.Reflect, "BorderType");

        #endregion

        #region 位置修正参数

        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        public ParamValue<bool> IsPositionCorrectionEnabled { get; set; } =
            ParamValue<bool>.CreateConstant(false, "IsPositionCorrectionEnabled");

        /// <summary>
        /// 位置修正绑定源（运行时，不序列化）
        /// </summary>
        [JsonIgnore]
        public AvailableDataSource? PositionCorrectionBindingSource
        {
            get => _positionCorrectionBindingSource;
            set
            {
                if (SetProperty(ref _positionCorrectionBindingSource, value, "位置修正绑定源"))
                {
                    _positionCorrectionSource = ImageSourceReference.FromDataSource(value);
                }
            }
        }

        /// <summary>
        /// 位置修正绑定源（序列化引用）
        /// </summary>
        [JsonPropertyOrder(16)]
        [JsonPropertyName("positionCorrectionSource")]
        public ImageSourceReference? PositionCorrectionSource { get; set; }

        /// <summary>
        /// 位置修正绑定源显示文本
        /// </summary>
        [JsonPropertyOrder(17)]
        public string? PositionCorrectionBindingDisplayText { get; set; }

        private AvailableDataSource? _positionCorrectionBindingSource;
        private ImageSourceReference? _positionCorrectionSource;

        /// <summary>
        /// 从 AvailableDataSources 列表恢复 PositionCorrectionBindingSource
        /// </summary>
        public bool RestorePositionCorrectionSource(IEnumerable<AvailableDataSource>? dataSources)
        {
            if (_positionCorrectionSource == null || dataSources == null)
                return false;

            var matched = _positionCorrectionSource.MatchDataSource(dataSources);
            if (matched != null)
            {
                _positionCorrectionBindingSource = matched;
                OnPropertyChanged(nameof(PositionCorrectionBindingSource));
                return true;
            }
            return false;
        }

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public ImageFilterResultConfig ResultConfig { get; set; } = new();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            // 内核大小必须为奇数
            if (KernelWidth.Value % 2 == 0)
                result.AddError($"内核宽度必须为奇数，当前值: {KernelWidth.Value}");

            if (KernelHeight.Value % 2 == 0)
                result.AddError($"内核高度必须为奇数，当前值: {KernelHeight.Value}");

            // 内核大小范围检查
            if (KernelWidth.Value < KernelMin || KernelWidth.Value > KernelMax)
                result.AddError($"内核宽度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelWidth.Value}");

            if (KernelHeight.Value < KernelMin || KernelHeight.Value > KernelMax)
                result.AddError($"内核高度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelHeight.Value}");

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

            if (ResultConfig.IsMeanCheckEnabled)
            {
                if (ResultConfig.MeanMin >= ResultConfig.MeanMax)
                    result.AddError($"输出均值最小值({ResultConfig.MeanMin})必须小于最大值({ResultConfig.MeanMax})");
            }
        }

        /// <summary>
        /// 获取滤波类型选项
        /// </summary>
        public static IReadOnlyList<string> GetFilterTypeOptions()
        {
            return new List<string> { "Blur", "Gaussian", "MedianBlur", "Bilateral", "Filter2D" };
        }

        /// <summary>
        /// 获取边界类型选项
        /// </summary>
        public static IReadOnlyList<string> GetBorderTypeOptions()
        {
            return new List<string> { "Reflect", "Replicate", "Constant", "Reflect101", "Wrap", "Isolated" };
        }

        /// <summary>
        /// 边界类型转 OpenCV BorderTypes
        /// </summary>
        public static OpenCvSharp.BorderTypes ToCvBorderType(ImageFilter.BorderType borderType)
        {
            return borderType switch
            {
                ImageFilter.BorderType.Reflect => OpenCvSharp.BorderTypes.Reflect,
                ImageFilter.BorderType.Replicate => OpenCvSharp.BorderTypes.Replicate,
                ImageFilter.BorderType.Constant => OpenCvSharp.BorderTypes.Constant,
                ImageFilter.BorderType.Reflect101 => OpenCvSharp.BorderTypes.Reflect101,
                ImageFilter.BorderType.Wrap => OpenCvSharp.BorderTypes.Wrap,
                ImageFilter.BorderType.Isolated => OpenCvSharp.BorderTypes.Isolated,
                _ => OpenCvSharp.BorderTypes.Reflect101
            };
        }
    }
}
