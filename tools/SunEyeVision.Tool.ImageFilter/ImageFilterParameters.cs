using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

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
    /// 参数类继承 RoiParameters，自带区域管理功能。
    /// 使用 纯 CLR 参数，统一管理参数值和绑定配置。
    ///
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "ImageFilter"。
    /// </remarks>
    [JsonDerivedType(typeof(ImageFilterParameters), "ImageFilter")]
    public class ImageFilterParameters : RoiParameters, IInputSlotContract, IRuntimeParameterContract
    {
        public IReadOnlyList<InputSlotDescriptor> GetInputSlots() => new[]
        {
            new InputSlotDescriptor(nameof(InspectionRois), "检测区域", InputSlotKind.Collection,
                ValueType: typeof(RoiCollection), Required: false),
            new InputSlotDescriptor(nameof(MaskRois), "屏蔽区域", InputSlotKind.Collection,
                ValueType: typeof(RoiCollection), Required: false)
        };

        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() => new[]
        {
            new RuntimeParameterDescriptor(nameof(KernelWidth), "内核宽度", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(KernelHeight), "内核高度", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(SigmaX), "Sigma X", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(SigmaY), "Sigma Y", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(SigmaColor), "颜色Sigma", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(SigmaSpace), "空间Sigma", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(CustomKernelText), "自定义核系数", "String", typeof(string)),
            new RuntimeParameterDescriptor(nameof(CustomKernelDivisor), "自定义核除数", "Double", typeof(double))
        };

        /// <summary>
        /// 构造函数 - 注册 FilterTypeValue 变更通知
        /// </summary>
        public ImageFilterParameters()
        {
        }

        /// <summary>
        /// 滤波类型变化时更新所有方法分组的可见性
        /// </summary>
        private void OnFilterTypeValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterTypeValue))
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

        /// <summary>
        /// 滤波类型后端字段
        /// </summary>
        private FilterType _filterTypeValue = FilterType.Blur;

        #region 运行参数

        /// <summary>
        /// 滤波类型
        /// </summary>
        [DisplayName("滤波类型")]
        public FilterType FilterTypeValue
        {
            get => _filterTypeValue;
            set
            {
                if (!ReferenceEquals(_filterTypeValue, value))
                {
                    _filterTypeValue = value;
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
        public bool IsBlurMethod => FilterTypeValue == FilterType.Blur;

        [JsonIgnore]
        public bool IsGaussianMethod => FilterTypeValue == FilterType.Gaussian;

        [JsonIgnore]
        public bool IsMedianBlurMethod => FilterTypeValue == FilterType.MedianBlur;

        [JsonIgnore]
        public bool IsBilateralMethod => FilterTypeValue == FilterType.Bilateral;

        [JsonIgnore]
        public bool IsFilter2DMethod => FilterTypeValue == FilterType.Filter2D;

        #endregion

        /// <summary>
        /// 内核宽度
        /// </summary>
        [DisplayName("内核宽度")]
        public int KernelWidth { get; set; } = 3;

        /// <summary>
        /// 内核高度
        /// </summary>
        [DisplayName("内核高度")]
        public int KernelHeight { get; set; } = 3;

        private bool _useCustomKernel;
        private string _customKernelText = CustomConvolutionKernel.DefaultText;
        private double _customKernelDivisor = 1;

        /// <summary>Explicit opt-in; missing fields in old solutions retain fixed sharpening.</summary>
        public bool UseCustomKernel
        {
            get => _useCustomKernel;
            set => SetProperty(ref _useCustomKernel, value);
        }

        public string CustomKernelText
        {
            get => _customKernelText;
            set => SetProperty(ref _customKernelText, value);
        }

        public double CustomKernelDivisor
        {
            get => _customKernelDivisor;
            set => SetProperty(ref _customKernelDivisor, value);
        }

        /// <summary>
        /// 高斯滤波 SigmaX
        /// </summary>
        [DisplayName("Sigma X")]
        public double SigmaX { get; set; }

        /// <summary>
        /// 高斯滤波 SigmaY
        /// </summary>
        [DisplayName("Sigma Y")]
        public double SigmaY { get; set; }

        /// <summary>
        /// 双边滤波颜色空间 Sigma
        /// </summary>
        [DisplayName("颜色Sigma")]
        public double SigmaColor { get; set; } = 10.0;

        /// <summary>
        /// 双边滤波坐标空间 Sigma
        /// </summary>
        [DisplayName("空间Sigma")]
        public double SigmaSpace { get; set; } = 10.0;

        /// <summary>
        /// 边界处理类型
        /// </summary>
        [DisplayName("边界类型")]
        public ImageFilter.BorderType BorderTypeValue { get; set; } = ImageFilter.BorderType.Reflect;

        #endregion

        #region 位置修正参数

        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        private bool _isPositionCorrectionEnabled;

        [DisplayName("启用位置修正")]
        public bool IsPositionCorrectionEnabled
        {
            get => _isPositionCorrectionEnabled;
            set => SetProperty(ref _isPositionCorrectionEnabled, value, "启用位置修正");
        }

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
        public ImageSourceReference? PositionCorrectionSource
        {
            get => _positionCorrectionSource;
            set => _positionCorrectionSource = value;
        }

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
        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            if (FilterTypeValue == FilterType.Filter2D && UseCustomKernel)
            {
                try { CustomConvolutionKernel.Parse(CustomKernelText); }
                catch (ArgumentException ex) { result.AddError(ex.Message); }
                if (!double.IsFinite(CustomKernelDivisor) || CustomKernelDivisor <= 0)
                    result.AddError("自定义核除数必须为有限正数；零和核请显式使用1，不会自动归一化");
            }

            // Custom Filter2D derives its dimensions from the matrix; preserve the
            // legacy width/height fields and bindings without letting inactive values resize it.
            if (!(FilterTypeValue == FilterType.Filter2D && UseCustomKernel))
            {
                // 内核大小必须为奇数
                if (KernelWidth % 2 == 0)
                    result.AddError($"内核宽度必须为奇数，当前值: {KernelWidth}");

                if (KernelHeight % 2 == 0)
                    result.AddError($"内核高度必须为奇数，当前值: {KernelHeight}");

                // 内核大小范围检查
                if (KernelWidth < KernelMin || KernelWidth > KernelMax)
                    result.AddError($"内核宽度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelWidth}");

                if (KernelHeight < KernelMin || KernelHeight > KernelMax)
                    result.AddError($"内核高度必须在 {KernelMin}-{KernelMax} 范围内，当前值: {KernelHeight}");
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
            config.Judgments.Add(new() { Key = "mean", Name = "输出均值", Enabled = false, Min = 0, Max = 255, AllowedMinimum = 0, AllowedMaximum = 255, DecimalPlaces = 1, Order = 20 });
            return config;
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
    }
}
