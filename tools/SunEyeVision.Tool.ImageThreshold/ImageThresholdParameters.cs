using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.ImageThreshold
{
    /// <summary>
    /// 阈值方法枚举 - 包含固定阈值、大津法、三角形法、自适应和范围阈值
    /// </summary>
    public enum ThresholdMethod
    {
        Binary = 0,
        BinaryInv = 1,
        Trunc = 2,
        ToZero = 3,
        ToZeroInv = 4,
        Otsu = 8,
        Triangle = 16,
        AdaptiveMean = 32,
        AdaptiveGaussian = 33,
        InRange = 64
    }

    /// <summary>
    /// 图像二值化工具参数
    /// </summary>
    /// <remarks>
    /// 继承 RoiParameters，自带区域管理功能。
    /// 参数使用纯 CLR 值；运行时绑定由执行请求中的参数快照承载。
    /// 
    /// 多态序列化（rule-010）：使用 [JsonDerivedType] 标识参数类型。
    /// </remarks>
    [JsonDerivedType(typeof(ImageThresholdParameters), "ImageThreshold")]
    public class ImageThresholdParameters : RoiParameters, IInputSlotContract, IRuntimeParameterContract
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
            new RuntimeParameterDescriptor(nameof(Threshold), "阈值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(MaxValue), "最大值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(Invert), "反转结果", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(BlockSize), "块大小", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(C), "C常数", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(MinThreshold), "最小阈值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(MaxThreshold), "最大阈值", "Integer", typeof(int))
        };

        /// <summary>
        /// 构造函数 - 注册 Method 变更通知
        /// </summary>
        public ImageThresholdParameters()
        {
        }

        /// <summary>
        /// Method 变化时更新所有方法分组的可见性
        /// </summary>
        private void OnMethodValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Method))
            {
                OnPropertyChanged(nameof(IsBinary));
                OnPropertyChanged(nameof(IsBinaryInv));
                OnPropertyChanged(nameof(IsTrunc));
                OnPropertyChanged(nameof(IsToZero));
                OnPropertyChanged(nameof(IsToZeroInv));
                OnPropertyChanged(nameof(IsOtsu));
                OnPropertyChanged(nameof(IsTriangle));
                OnPropertyChanged(nameof(IsAdaptiveMean));
                OnPropertyChanged(nameof(IsAdaptiveGaussian));
                OnPropertyChanged(nameof(IsInRange));
            }
        }

        #region 常量定义

        /// <summary>阈值最小值</summary>
        public const int ThresholdMin = 0;

        /// <summary>阈值最大值</summary>
        public const int ThresholdMax = 255;

        /// <summary>块大小最小值</summary>
        public const int BlockSizeMin = 3;

        /// <summary>块大小最大值</summary>
        public const int BlockSizeMax = 31;

        /// <summary>C常数最小值</summary>
        public const double CMin = 0;

        /// <summary>C常数最大值</summary>
        public const double CMax = 100;

        #endregion

        #region 基本参数（继承自 RoiParameters）

        // ImageSource - 输入图像源（继承）
        // InspectionRois / MaskRois - 区域编辑（继承）

        #endregion

        #region 位置修正参数

        private bool _isPositionCorrectionEnabled;
        /// <summary>
        /// 是否启用位置修正
        /// </summary>
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

        #region 方法可见性属性（运行时计算，每种方法独立一个Group）

        [JsonIgnore] public bool IsBinary => Method == ThresholdMethod.Binary;
        [JsonIgnore] public bool IsBinaryInv => Method == ThresholdMethod.BinaryInv;
        [JsonIgnore] public bool IsTrunc => Method == ThresholdMethod.Trunc;
        [JsonIgnore] public bool IsToZero => Method == ThresholdMethod.ToZero;
        [JsonIgnore] public bool IsToZeroInv => Method == ThresholdMethod.ToZeroInv;
        [JsonIgnore] public bool IsOtsu => Method == ThresholdMethod.Otsu;
        [JsonIgnore] public bool IsTriangle => Method == ThresholdMethod.Triangle;
        [JsonIgnore] public bool IsAdaptiveMean => Method == ThresholdMethod.AdaptiveMean;
        [JsonIgnore] public bool IsAdaptiveGaussian => Method == ThresholdMethod.AdaptiveGaussian;
        [JsonIgnore] public bool IsInRange => Method == ThresholdMethod.InRange;

        #endregion

        /// <summary>
        /// 阈值方法后端字段
        /// </summary>
        private ThresholdMethod _method = ThresholdMethod.Binary;

        #region 运行参数

        /// <summary>
        /// 阈值方法
        /// </summary>
        [DisplayName("阈值方法")]
        public ThresholdMethod Method
        {
            get => _method;
            set
            {
                if (!ReferenceEquals(_method, value))
                {
                    _method = value;
                    OnPropertyChanged();
                    // 方法实例被替换时也刷新所有可见性
                    OnPropertyChanged(nameof(IsBinary));
                    OnPropertyChanged(nameof(IsBinaryInv));
                    OnPropertyChanged(nameof(IsTrunc));
                    OnPropertyChanged(nameof(IsToZero));
                    OnPropertyChanged(nameof(IsToZeroInv));
                    OnPropertyChanged(nameof(IsOtsu));
                    OnPropertyChanged(nameof(IsTriangle));
                    OnPropertyChanged(nameof(IsAdaptiveMean));
                    OnPropertyChanged(nameof(IsAdaptiveGaussian));
                    OnPropertyChanged(nameof(IsInRange));
                }
            }
        }

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
        /// 是否反转二值化结果
        /// </summary>
        private bool _invert;

        [DisplayName("反转结果")]
        public bool Invert
        {
            get => _invert;
            set => SetProperty(ref _invert, value, "反转结果");
        }

        /// <summary>
        /// 自适应阈值块大小(奇数, 3-31)
        /// </summary>
        [DisplayName("块大小")]
        public int BlockSize { get; set; } = 11;

        /// <summary>
        /// 自适应阈值C常数(0-100)
        /// </summary>
        [DisplayName("C常数")]
        public double C { get; set; } = 2.0;

        /// <summary>
        /// InRange最小阈值(0-255)
        /// </summary>
        [DisplayName("最小阈值")]
        public int MinThreshold { get; set; }

        /// <summary>
        /// InRange最大阈值(0-255)
        /// </summary>
        [DisplayName("最大阈值")]
        public int MaxThreshold { get; set; } = 255;

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

            var method = Method;

            // 块大小验证（自适应模式）
            if (method == ThresholdMethod.AdaptiveMean || method == ThresholdMethod.AdaptiveGaussian)
            {
                if (BlockSize % 2 == 0)
                {
                    result.AddError($"块大小必须为奇数，当前值: {BlockSize}");
                }

                if (BlockSize < BlockSizeMin || BlockSize > BlockSizeMax)
                {
                    result.AddError($"块大小必须在 {BlockSizeMin}-{BlockSizeMax} 范围内，当前值: {BlockSize}");
                }

                if (C < CMin || C > CMax)
                {
                    result.AddError($"C常数必须在 {CMin}-{CMax} 范围内，当前值: {C}");
                }
            }

            // 阈值范围检查（固定阈值模式）
            if (method == ThresholdMethod.Binary || method == ThresholdMethod.BinaryInv ||
                method == ThresholdMethod.Trunc || method == ThresholdMethod.ToZero ||
                method == ThresholdMethod.ToZeroInv)
            {
                if (Threshold < ThresholdMin || Threshold > ThresholdMax)
                {
                    result.AddError($"阈值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {Threshold}");
                }
            }

            // 最大值范围检查
            if (method != ThresholdMethod.InRange)
            {
                if (MaxValue < ThresholdMin || MaxValue > ThresholdMax)
                {
                    result.AddError($"最大值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {MaxValue}");
                }
            }

            // InRange 范围检查
            if (method == ThresholdMethod.InRange)
            {
                if (MinThreshold >= MaxThreshold)
                {
                    result.AddError($"最小阈值({MinThreshold})必须小于最大阈值({MaxThreshold})");
                }
                if (MinThreshold < ThresholdMin || MinThreshold > ThresholdMax ||
                    MaxThreshold < ThresholdMin || MaxThreshold > ThresholdMax)
                {
                    result.AddError($"InRange阈值范围必须在 {ThresholdMin}-{ThresholdMax} 之间");
                }
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
            return config;
        }
    }
}
