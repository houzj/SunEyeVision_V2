using System.Windows.Media;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Blob排序方式枚举
    /// </summary>
    public enum BlobSortMode
    {
        /// <summary>不排序（保持原始连通域顺序）</summary>
        None = 0,
        /// <summary>面积从小到大</summary>
        AreaAsc = 1,
        /// <summary>面积从大到小</summary>
        AreaDesc = 2,
        /// <summary>圆度从小到大</summary>
        CircularityAsc = 3,
        /// <summary>圆度从大到小</summary>
        CircularityDesc = 4,
        /// <summary>矩形度从小到大</summary>
        RectangularityAsc = 5,
        /// <summary>矩形度从大到小</summary>
        RectangularityDesc = 6,
        /// <summary>质心X坐标从小到大</summary>
        XAsc = 7,
        /// <summary>质心X坐标从大到小</summary>
        XDesc = 8,
        /// <summary>质心Y坐标从小到大</summary>
        YAsc = 9,
        /// <summary>质心Y坐标从大到小</summary>
        YDesc = 10
    }

    /// <summary>
    /// 图像预处理方法枚举
    /// </summary>
    public enum PreprocessMethod
    {
        None = 0,
        Mean = 1,
        Gaussian = 2,
        Median = 3,
        HistogramEqualization = 4
    }

    /// <summary>
    /// 形态学操作类型枚举
    /// </summary>
    public enum MorphOperationType
    {
        Erosion = 0,
        Dilation = 1,
        Opening = 2,
        Closing = 3,
        TopHat = 4,
        BottomHat = 5
    }

    /// <summary>
    /// 形态学结构元素形状枚举
    /// </summary>
    public enum MorphElementShapeType
    {
        Rectangle = 0,
        Circle = 1
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
        [DisplayName("阈值")]
        public ParamValue<int> Threshold { get; set; } =
            ParamValue<int>.CreateConstant(128, "Threshold");

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        [DisplayName("最大值")]
        public ParamValue<int> MaxValue { get; set; } =
            ParamValue<int>.CreateConstant(255, "MaxValue");

        /// <summary>
        /// 二值化方法（仅支持 Binary/BinaryInv）
        /// </summary>
        [DisplayName("二值化方法")]
        public ParamValue<ThresholdType> Type { get; set; } =
            ParamValue<ThresholdType>.CreateConstant(ThresholdType.Binary, "Type");

        #endregion

        #region 图像预处理参数

        /// <summary>
        /// 是否启用图像预处理
        /// </summary>
        [DisplayName("启用预处理")]
        public bool IsPreprocessingEnabled
        {
            get => _isPreprocessingEnabled;
            set => SetProperty(ref _isPreprocessingEnabled, value, "启用预处理");
        }

        /// <summary>
        /// 预处理方法
        /// </summary>
        [DisplayName("预处理方法")]
        public ParamValue<PreprocessMethod> PreprocessMethodValue { get; set; } =
            ParamValue<PreprocessMethod>.CreateConstant(PreprocessMethod.None, "PreprocessMethodValue");

        /// <summary>
        /// 预处理滤波核大小（均值/高斯/中值滤波，奇数3~31）
        /// </summary>
        [DisplayName("滤波核大小")]
        public ParamValue<int> PreprocessKernelSize { get; set; } =
            ParamValue<int>.CreateConstant(5, "PreprocessKernelSize");

        #endregion

        #region 形态学处理参数

        /// <summary>
        /// 是否启用形态学处理
        /// </summary>
        [DisplayName("启用形态学")]
        public bool IsMorphologyEnabled
        {
            get => _isMorphologyEnabled;
            set => SetProperty(ref _isMorphologyEnabled, value, "启用形态学");
        }

        /// <summary>
        /// 形态学操作类型
        /// </summary>
        [DisplayName("形态学操作")]
        public ParamValue<MorphOperationType> MorphOperationValue { get; set; } =
            ParamValue<MorphOperationType>.CreateConstant(MorphOperationType.Erosion, "MorphOperationValue");

        /// <summary>
        /// 结构元素形状
        /// </summary>
        [DisplayName("结构元素形状")]
        public ParamValue<MorphElementShapeType> MorphElementShapeValue { get; set; } =
            ParamValue<MorphElementShapeType>.CreateConstant(MorphElementShapeType.Rectangle, "MorphElementShapeValue");

        /// <summary>
        /// 结构元素宽度(1~101)
        /// </summary>
        [DisplayName("结构元素宽度")]
        public ParamValue<int> MorphElementWidth { get; set; } =
            ParamValue<int>.CreateConstant(5, "MorphElementWidth");

        /// <summary>
        /// 结构元素高度(1~101)
        /// </summary>
        [DisplayName("结构元素高度")]
        public ParamValue<int> MorphElementHeight { get; set; } =
            ParamValue<int>.CreateConstant(5, "MorphElementHeight");

        /// <summary>
        /// 形态学迭代次数(1~10)
        /// </summary>
        [DisplayName("迭代次数")]
        public ParamValue<int> MorphIterations { get; set; } =
            ParamValue<int>.CreateConstant(1, "MorphIterations");

        #endregion

        #region Blob 筛选参数（Tab3：独立于 Tab4 判断的筛选条件）

        /// <summary>启用面积筛选</summary>
        [DisplayName("启用面积筛选")]
        public bool IsAreaFilterEnabled
        {
            get => _isAreaFilterEnabled;
            set => SetProperty(ref _isAreaFilterEnabled, value, "启用面积筛选");
        }

        /// <summary>面积筛选最小值</summary>
        [DisplayName("面积最小值")]
        public double FilterAreaMin
        {
            get => _filterAreaMin;
            set => SetProperty(ref _filterAreaMin, value, "面积最小值");
        }

        /// <summary>面积筛选最大值</summary>
        [DisplayName("面积最大值")]
        public double FilterAreaMax
        {
            get => _filterAreaMax;
            set => SetProperty(ref _filterAreaMax, value, "面积最大值");
        }

        /// <summary>启用圆度筛选</summary>
        [DisplayName("启用圆度筛选")]
        public bool IsCircularityFilterEnabled
        {
            get => _isCircularityFilterEnabled;
            set => SetProperty(ref _isCircularityFilterEnabled, value, "启用圆度筛选");
        }

        /// <summary>圆度筛选最小值</summary>
        [DisplayName("圆度最小值")]
        public double FilterCircularityMin
        {
            get => _filterCircularityMin;
            set => SetProperty(ref _filterCircularityMin, value, "圆度最小值");
        }

        /// <summary>圆度筛选最大值</summary>
        [DisplayName("圆度最大值")]
        public double FilterCircularityMax
        {
            get => _filterCircularityMax;
            set => SetProperty(ref _filterCircularityMax, value, "圆度最大值");
        }

        /// <summary>启用矩形度筛选</summary>
        [DisplayName("启用矩形度筛选")]
        public bool IsRectangularityFilterEnabled
        {
            get => _isRectangularityFilterEnabled;
            set => SetProperty(ref _isRectangularityFilterEnabled, value, "启用矩形度筛选");
        }

        /// <summary>矩形度筛选最小值</summary>
        [DisplayName("矩形度最小值")]
        public double FilterRectangularityMin
        {
            get => _filterRectangularityMin;
            set => SetProperty(ref _filterRectangularityMin, value, "矩形度最小值");
        }

        /// <summary>矩形度筛选最大值</summary>
        [DisplayName("矩形度最大值")]
        public double FilterRectangularityMax
        {
            get => _filterRectangularityMax;
            set => SetProperty(ref _filterRectangularityMax, value, "矩形度最大值");
        }

        #endregion

        #region 显示样式

        /// <summary>
        /// 结果判断配置
        /// </summary>
        public BlobAnalysisResultConfig ResultConfig { get; set; } = new();

        #region 排序方式

        /// <summary>
        /// Blob排序方式
        /// </summary>
        [DisplayName("排序方式")]
        public ParamValue<BlobSortMode> SortMode { get; set; } =
            ParamValue<BlobSortMode>.CreateConstant(BlobSortMode.AreaDesc, "SortMode");

        #endregion

        /// <summary>外接矩形样式</summary>
        public GraphicsStyle BlobsStyle { get; set; } = new() { Name = "Blobs", LineWidth = 2 };

        /// <summary>质心标记样式</summary>
        public GraphicsStyle CentroidsStyle { get; set; } = new() { Name = "Centroids", LineWidth = 2, CrossSize = 8 };

        /// <summary>轮廓样式</summary>
        public GraphicsStyle ContoursStyle { get; set; } = new() { Name = "Contours", LineWidth = 1 };

        /// <summary>面积列表样式</summary>
        public TextStyle AreaDisplayStyle { get; set; } = new() { Name = "AreaDisplay", FontSize = 9.0, PositionX = 0, PositionY = 0 };

        /// <summary>检测区域边框样式（默认蓝色 #0000FF，与 ROI 编辑器检测区域一致）</summary>
        public override GraphicsStyle InspectionRegionStyle { get; set; } = new()
        {
            Name = "InspectionRegion",
            LineWidth = 1,
            OkColor = Color.FromRgb(0, 0, 255)
        };

        /// <summary>屏蔽区域边框样式（默认橙色 #FFA500，与 ROI 编辑器屏蔽区域一致）</summary>
        public override GraphicsStyle MaskRegionStyle { get; set; } = new()
        {
            Name = "MaskRegion",
            LineWidth = 1,
            OkColor = Color.FromRgb(0xFF, 0xA5, 0x00)
        };

        #endregion

        #region 位置修正

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

        private bool _isPreprocessingEnabled;
        private bool _isMorphologyEnabled;
        private double _filterAreaMin = 10;
        private double _filterAreaMax = 999999;
        private double _filterCircularityMin;
        private double _filterCircularityMax = 1.0;
        private double _filterRectangularityMin;
        private double _filterRectangularityMax = 1.0;
        private bool _isAreaFilterEnabled = true;
        private bool _isCircularityFilterEnabled;
        private bool _isRectangularityFilterEnabled;
        private bool _isPositionCorrectionEnabled;
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

            // 预处理参数验证
            if (IsPreprocessingEnabled && PreprocessMethodValue.Value != PreprocessMethod.None)
            {
                int kernelSize = PreprocessKernelSize.Value;
                if (kernelSize < 3 || kernelSize > 31 || kernelSize % 2 == 0)
                {
                    result.AddError($"滤波核大小必须为 3~31 之间的奇数，当前值: {kernelSize}");
                }
            }

            // 形态学参数验证
            if (IsMorphologyEnabled)
            {
                if (MorphElementWidth.Value < 1 || MorphElementWidth.Value > 101)
                {
                    result.AddError($"结构元素宽度必须在 1~101 范围内，当前值: {MorphElementWidth.Value}");
                }
                if (MorphElementHeight.Value < 1 || MorphElementHeight.Value > 101)
                {
                    result.AddError($"结构元素高度必须在 1~101 范围内，当前值: {MorphElementHeight.Value}");
                }
                if (MorphIterations.Value < 1 || MorphIterations.Value > 10)
                {
                    result.AddError($"迭代次数必须在 1~10 范围内，当前值: {MorphIterations.Value}");
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

        /// <summary>
        /// 获取排序方式选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetSortModeOptions()
        {
            return new List<string> { "不排序", "面积 ↑", "面积 ↓", "圆度 ↑", "圆度 ↓", "矩形度 ↑", "矩形度 ↓", "X坐标 ↑", "X坐标 ↓", "Y坐标 ↑", "Y坐标 ↓" };
        }

        /// <summary>
        /// 获取预处理方法选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetPreprocessMethodOptions()
        {
            return new List<string> { "无", "均值滤波", "高斯滤波", "中值滤波", "直方图均衡化" };
        }

        /// <summary>
        /// 获取形态学操作选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetMorphOperationOptions()
        {
            return new List<string> { "腐蚀", "膨胀", "开运算", "闭运算", "顶帽", "黑帽" };
        }

        /// <summary>
        /// 获取结构元素形状选项（中文）
        /// </summary>
        public static IReadOnlyList<string> GetMorphElementShapeOptions()
        {
            return new List<string> { "矩形", "圆形" };
        }
    }
}
