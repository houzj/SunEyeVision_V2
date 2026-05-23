using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ImageThreshold.Models;

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
    /// 继承 RegionParameters，自带区域管理功能。
    /// 使用 ParamValue 包装器，统一管理参数值和绑定配置。
    /// 
    /// 多态序列化（rule-010）：使用 [JsonDerivedType] 标识参数类型。
    /// </remarks>
    [JsonDerivedType(typeof(ImageThresholdParameters), "ImageThreshold")]
    public class ImageThresholdParameters : RegionParameters
    {
        /// <summary>
        /// 构造函数 - 注册 Method 变更通知
        /// </summary>
        public ImageThresholdParameters()
        {
            _method.PropertyChanged += OnMethodValueChanged;
        }

        /// <summary>
        /// Method.Value 变化时更新所有方法分组的可见性
        /// </summary>
        private void OnMethodValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParamValue<int>.Value))
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

        #region 基本参数（继承自 RegionParameters）

        // ImageSource - 输入图像源（继承）
        // IsRegionEnabled - 启用区域限制（继承）
        // InspectionRegions / MaskRegions - 区域编辑（继承）

        #endregion

        #region 位置修正参数

        private ParamValue<bool> _isPositionCorrectionEnabled = ParamValue<bool>.CreateConstant(false, "IsPositionCorrectionEnabled");
        /// <summary>
        /// 是否启用位置修正
        /// </summary>
        public ParamValue<bool> IsPositionCorrectionEnabled
        {
            get => _isPositionCorrectionEnabled;
            set => _isPositionCorrectionEnabled = value;
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

        #region 方法可见性属性（运行时计算，每种方法独立一个Group）

        [JsonIgnore] public bool IsBinary => Method.Value == ThresholdMethod.Binary;
        [JsonIgnore] public bool IsBinaryInv => Method.Value == ThresholdMethod.BinaryInv;
        [JsonIgnore] public bool IsTrunc => Method.Value == ThresholdMethod.Trunc;
        [JsonIgnore] public bool IsToZero => Method.Value == ThresholdMethod.ToZero;
        [JsonIgnore] public bool IsToZeroInv => Method.Value == ThresholdMethod.ToZeroInv;
        [JsonIgnore] public bool IsOtsu => Method.Value == ThresholdMethod.Otsu;
        [JsonIgnore] public bool IsTriangle => Method.Value == ThresholdMethod.Triangle;
        [JsonIgnore] public bool IsAdaptiveMean => Method.Value == ThresholdMethod.AdaptiveMean;
        [JsonIgnore] public bool IsAdaptiveGaussian => Method.Value == ThresholdMethod.AdaptiveGaussian;
        [JsonIgnore] public bool IsInRange => Method.Value == ThresholdMethod.InRange;

        #endregion

        #region 运行参数

        /// <summary>
        /// 阈值方法（字段后备属性，反序列化替换实例时自动恢复事件订阅）
        /// </summary>
        private ParamValue<ThresholdMethod> _method = ParamValue<ThresholdMethod>.CreateConstant(ThresholdMethod.Binary, "Method");

        public ParamValue<ThresholdMethod> Method
        {
            get => _method;
            set
            {
                if (!ReferenceEquals(_method, value))
                {
                    if (_method != null)
                        _method.PropertyChanged -= OnMethodValueChanged;
                    _method = value;
                    if (_method != null)
                        _method.PropertyChanged += OnMethodValueChanged;
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
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 是否反转二值化结果
        /// </summary>
        public ParamValue<bool> Invert { get; set; } =
            ParamValue<bool>.CreateConstant(false, "Invert");

        /// <summary>
        /// 自适应阈值块大小(奇数, 3-31)
        /// </summary>
        public ParamValue<int> BlockSize { get; set; } =
            ParamValue<int>.CreateConstant(11, "BlockSize");

        /// <summary>
        /// 自适应阈值C常数(0-100)
        /// </summary>
        public ParamValue<double> C { get; set; } =
            ParamValue<double>.CreateConstant(2.0, "C");

        /// <summary>
        /// InRange最小阈值(0-255)
        /// </summary>
        public ParamValue<int> MinThreshold { get; set; } =
            ParamValue<int>.CreateConstant(0, "MinThreshold");

        /// <summary>
        /// InRange最大阈值(0-255)
        /// </summary>
        public ParamValue<int> MaxThreshold { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxThreshold");

        #endregion

        #region 结果显示配置

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public ImageThresholdResultConfig ResultConfig { get; set; } = new();

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

            var method = Method.Value;

            // 块大小验证（自适应模式）
            if (method == ThresholdMethod.AdaptiveMean || method == ThresholdMethod.AdaptiveGaussian)
            {
                if (BlockSize.Value % 2 == 0)
                {
                    result.AddError($"块大小必须为奇数，当前值: {BlockSize.Value}");
                }

                if (BlockSize.Value < BlockSizeMin || BlockSize.Value > BlockSizeMax)
                {
                    result.AddError($"块大小必须在 {BlockSizeMin}-{BlockSizeMax} 范围内，当前值: {BlockSize.Value}");
                }

                if (C.Value < CMin || C.Value > CMax)
                {
                    result.AddError($"C常数必须在 {CMin}-{CMax} 范围内，当前值: {C.Value}");
                }
            }

            // 阈值范围检查（固定阈值模式）
            if (method == ThresholdMethod.Binary || method == ThresholdMethod.BinaryInv ||
                method == ThresholdMethod.Trunc || method == ThresholdMethod.ToZero ||
                method == ThresholdMethod.ToZeroInv)
            {
                if (Threshold.Value < ThresholdMin || Threshold.Value > ThresholdMax)
                {
                    result.AddError($"阈值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {Threshold.Value}");
                }
            }

            // 最大值范围检查
            if (method != ThresholdMethod.InRange)
            {
                if (MaxValue.Value < ThresholdMin || MaxValue.Value > ThresholdMax)
                {
                    result.AddError($"最大值必须在 {ThresholdMin}-{ThresholdMax} 范围内，当前值: {MaxValue.Value}");
                }
            }

            // InRange 范围检查
            if (method == ThresholdMethod.InRange)
            {
                if (MinThreshold.Value >= MaxThreshold.Value)
                {
                    result.AddError($"最小阈值({MinThreshold.Value})必须小于最大阈值({MaxThreshold.Value})");
                }
                if (MinThreshold.Value < ThresholdMin || MinThreshold.Value > ThresholdMax ||
                    MaxThreshold.Value < ThresholdMin || MaxThreshold.Value > ThresholdMax)
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
            if (ResultConfig == null)
            {
                result.AddError("结果判断配置不能为空");
                return;
            }

            if (ResultConfig.IsWhitePixelRatioCheckEnabled)
            {
                if (ResultConfig.WhitePixelRatioMin >= ResultConfig.WhitePixelRatioMax)
                {
                    result.AddError($"白色像素比例最小值({ResultConfig.WhitePixelRatioMin})必须小于最大值({ResultConfig.WhitePixelRatioMax})");
                }
            }

            if (ResultConfig.IsMeanCheckEnabled)
            {
                if (ResultConfig.MeanMin >= ResultConfig.MeanMax)
                {
                    result.AddError($"输出均值最小值({ResultConfig.MeanMin})必须小于最大值({ResultConfig.MeanMax})");
                }
            }

            if (ResultConfig.IsAreaCheckEnabled)
            {
                if (ResultConfig.AreaMin >= ResultConfig.AreaMax)
                {
                    result.AddError($"输出面积最小值({ResultConfig.AreaMin})必须小于最大值({ResultConfig.AreaMax})");
                }
            }
        }
    }
}
