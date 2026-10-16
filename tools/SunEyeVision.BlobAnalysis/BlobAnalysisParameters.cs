using System.Linq;
using System.Windows.Media;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Validation;

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
    /// 参数类继承 RoiParameters，自带区域管理功能。
    /// 使用 纯 CLR 参数，统一管理参数值和绑定配置。
    ///
    /// 多态序列化（rule-010: 方案系统实现规范）：
    /// 使用 [JsonDerivedType] 特性标识参数类型，类型标识符为 "BlobAnalysis"。
    /// </remarks>
    [JsonDerivedType(typeof(BlobAnalysisParameters), "BlobAnalysis")]
    public class BlobAnalysisParameters : RoiParameters, IInputSlotContract, IRuntimeParameterContract
    {
        /// <summary>
        /// Blob 分析的输入与运行参数契约。
        /// 这些描述是参数模型的唯一语义来源，UI 只负责展示和选择绑定。
        /// </summary>
        public IReadOnlyList<InputSlotDescriptor> GetInputSlots() => new[]
        {
            new InputSlotDescriptor(
                nameof(InspectionRois), "检测区域", InputSlotKind.Collection,
                ValueType: typeof(RoiCollection), Required: false),
            new InputSlotDescriptor(
                nameof(MaskRois), "屏蔽区域", InputSlotKind.Collection,
                ValueType: typeof(RoiCollection), Required: false)
        };

        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() => new[]
        {
            new RuntimeParameterDescriptor(nameof(Threshold), "阈值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(MaxValue), "最大值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(Type), "二值化方法", ValueType: typeof(ThresholdType)),
            new RuntimeParameterDescriptor(nameof(IsPreprocessingEnabled), "启用预处理", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(PreprocessMethodValue), "预处理方法", ValueType: typeof(PreprocessMethod)),
            new RuntimeParameterDescriptor(nameof(PreprocessKernelSize), "滤波核大小", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(IsMorphologyEnabled), "启用形态学", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(MorphOperationValue), "形态学操作", ValueType: typeof(MorphOperationType)),
            new RuntimeParameterDescriptor(nameof(MorphElementShapeValue), "结构元素形状", ValueType: typeof(MorphElementShapeType)),
            new RuntimeParameterDescriptor(nameof(MorphElementWidth), "结构元素宽度", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(MorphElementHeight), "结构元素高度", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(MorphIterations), "迭代次数", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(IsAreaFilterEnabled), "启用面积筛选", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(FilterAreaMin), "面积最小值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(FilterAreaMax), "面积最大值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(IsCircularityFilterEnabled), "启用圆度筛选", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(FilterCircularityMin), "圆度最小值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(FilterCircularityMax), "圆度最大值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(IsRectangularityFilterEnabled), "启用矩形度筛选", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(FilterRectangularityMin), "矩形度最小值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(FilterRectangularityMax), "矩形度最大值", "Double", typeof(double)),
            new RuntimeParameterDescriptor(nameof(SortMode), "排序方式", ValueType: typeof(BlobSortMode)),
            new RuntimeParameterDescriptor(nameof(IsBlobCountFilterEnabled), "启用查找数量筛选", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(BlobCountLimit), "查找数量", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(OutputBlobImage), "Blob图像输出", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(OutputBinaryImage), "二值化图像输出", "Boolean", typeof(bool)),
            new RuntimeParameterDescriptor(nameof(OutputContours), "轮廓输出", "Boolean", typeof(bool))
        };

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
        public int Threshold { get; set; } = 128;

        /// <summary>
        /// 超过阈值时使用的最大值(0-255)
        /// </summary>
        [DisplayName("最大值")]
        public int MaxValue { get; set; } = 255;

        /// <summary>
        /// 二值化方法（仅支持 Binary/BinaryInv）
        /// </summary>
        [DisplayName("二值化方法")]
        public ThresholdType Type { get; set; } = ThresholdType.Binary;

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
        public PreprocessMethod PreprocessMethodValue { get; set; } = PreprocessMethod.None;

        /// <summary>
        /// 预处理滤波核大小（均值/高斯/中值滤波，奇数3~31）
        /// </summary>
        [DisplayName("滤波核大小")]
        public int PreprocessKernelSize { get; set; } = 5;

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
        public MorphOperationType MorphOperationValue { get; set; } = MorphOperationType.Erosion;

        /// <summary>
        /// 结构元素形状
        /// </summary>
        [DisplayName("结构元素形状")]
        public MorphElementShapeType MorphElementShapeValue { get; set; } = MorphElementShapeType.Rectangle;

        /// <summary>
        /// 结构元素宽度(1~101)
        /// </summary>
        [DisplayName("结构元素宽度")]
        public int MorphElementWidth { get; set; } = 5;

        /// <summary>
        /// 结构元素高度(1~101)
        /// </summary>
        [DisplayName("结构元素高度")]
        public int MorphElementHeight { get; set; } = 5;

        /// <summary>
        /// 形态学迭代次数(1~10)
        /// </summary>
        [DisplayName("迭代次数")]
        public int MorphIterations { get; set; } = 1;

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
        public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

        /// <summary>
        /// 是否限制后续结果、区域和显示统一使用的 Blob 数量。
        /// </summary>
        [DisplayName("启用查找数量筛选")]
        public bool IsBlobCountFilterEnabled { get; set; } = true;

        [DisplayName("查找数量")]
        public int BlobCountLimit { get; set; } = 1000;

        [DisplayName("Blob图像输出")]
        public bool OutputBlobImage { get; set; } = true;

        [DisplayName("二值化图像输出")]
        public bool OutputBinaryImage { get; set; }

        [DisplayName("轮廓输出")]
        public bool OutputContours { get; set; }

        public const int BlobCountLimitMin = 1;
        public const int BlobCountLimitMax = 20000;

        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            config.Judgments.Add(new() { Key = "output-not-empty", Name = "输出不为空", RuleKind = JudgmentRuleKind.Boolean, Enabled = false, Order = 10 });
            config.Judgments.Add(new() { Key = "white-ratio", Name = "白色像素比例", Enabled = false, Min = 0, Max = 100, Unit = "%", AllowedMinimum = 0, AllowedMaximum = 100, Order = 20 });
            config.Judgments.Add(new() { Key = "blob-count", Name = "Blob数量", Enabled = false, Min = 0, Max = 9999, AllowedMinimum = 0, AllowedMaximum = 999999, DecimalPlaces = 0, Order = 30 });
            config.Judgments.Add(new() { Key = "total-area", Name = "总面积", Enabled = false, Min = 0, Max = 10000000, Unit = "px²", AllowedMinimum = 0, Order = 40 });
            config.Judgments.Add(new() { Key = "average-area", Name = "平均面积", Enabled = false, Min = 0, Max = 10000000, Unit = "px²", AllowedMinimum = 0, Order = 50 });
            config.Judgments.Add(new() { Key = "max-area", Name = "最大面积", Enabled = false, Min = 0, Max = 10000000, Unit = "px²", AllowedMinimum = 0, Order = 60 });
            config.Judgments.Add(new() { Key = "min-area", Name = "最小面积", Enabled = false, Min = 0, Max = 10000000, Unit = "px²", AllowedMinimum = 0, Order = 70 });
            config.Judgments.Add(new() { Key = "average-circularity", Name = "平均圆度", Enabled = false, Min = 0, Max = 1, AllowedMinimum = 0, AllowedMaximum = 1, DecimalPlaces = 3, Order = 80 });
            config.Judgments.Add(new() { Key = "average-rectangularity", Name = "平均矩形度", Enabled = false, Min = 0, Max = 1, AllowedMinimum = 0, AllowedMaximum = 1, DecimalPlaces = 3, Order = 90 });
            config.Judgments.Add(new() { Key = "per-blob-area", Name = "每个Blob面积", Scope = JudgmentScope.PerItem, AffectsItemDisplayState = true, Enabled = false, Min = 0, Max = 10000000, Unit = "px²", AllowedMinimum = 0, Order = 100 });
            config.Judgments.Add(new() { Key = "per-blob-circularity", Name = "每个Blob圆度", Scope = JudgmentScope.PerItem, AffectsItemDisplayState = true, Enabled = false, Min = 0, Max = 1, AllowedMinimum = 0, AllowedMaximum = 1, DecimalPlaces = 3, Order = 110 });
            config.Judgments.Add(new() { Key = "per-blob-rectangularity", Name = "每个Blob矩形度", Scope = JudgmentScope.PerItem, AffectsItemDisplayState = true, Enabled = false, Min = 0, Max = 1, AllowedMinimum = 0, AllowedMaximum = 1, DecimalPlaces = 3, Order = 120 });
            config.Graphics.Add(new() { Key = "blobs", Name = "外接矩形", Order = 10, Style = new() { Name = "Blobs", LineWidth = 2 } });
            config.Graphics.Add(new() { Key = "centroids", Name = "质心", Order = 20, Style = new() { Name = "Centroids", LineWidth = 2, CrossSize = 8, IsVisible = false } });
            config.Graphics.Add(new() { Key = "contours", Name = "轮廓", Order = 30, Style = new() { Name = "Contours", LineWidth = 1, IsVisible = false } });
            config.Graphics.Add(new() { Key = "inspection-roi", Name = "检测区", Order = 40, Style = new() { Name = "InspectionRoi", LineWidth = 1, OkColor = Color.FromRgb(0, 0, 255) } });
            config.Graphics.Add(new() { Key = "mask-roi", Name = "屏蔽区", Order = 50, Style = new() { Name = "MaskRoi", LineWidth = 1, OkColor = Color.FromRgb(0xFF, 0xA5, 0x00) } });
            config.Texts.Add(new() { Key = "area", Name = "面积列表", Order = 10, Style = new() { Name = "AreaDisplay", FontSize = 9.0, PositionX = 0, PositionY = 0 } });
            return config;
        }

        #region 排序方式

        /// <summary>
        /// Blob排序方式
        /// </summary>
        [DisplayName("排序方式")]
        public BlobSortMode SortMode { get; set; } = BlobSortMode.AreaDesc;

        #endregion

        /// <summary>外接矩形样式</summary>


        /// <summary>检测区边框样式（默认蓝色 #0000FF，与 ROI 编辑器检测区一致）</summary>
        public override GraphicsStyle InspectionRoiStyle
        {
            get => ResultPresentation.GetGraphicsStyle("inspection-roi");
            set => ResultPresentation.Graphics.Single(item => item.Key == "inspection-roi").Style = value;
        }

        /// <summary>屏蔽区边框样式（默认橙色 #FFA500，与 ROI 编辑器屏蔽区一致）</summary>
        public override GraphicsStyle MaskRoiStyle
        {
            get => ResultPresentation.GetGraphicsStyle("mask-roi");
            set => ResultPresentation.Graphics.Single(item => item.Key == "mask-roi").Style = value;
        }

        #endregion

        #region 字段（预处理/形态学/滤波，位置修正字段已上提基类 RoiParameters）

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

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();

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

            // Halcon threshold 的上下限必须构成有效区间。此前只校验了两个端点的取值范围，
            // 允许 Threshold > MaxValue，运行时会得到与界面配置不一致的分割结果。
            if (Threshold > MaxValue)
            {
                result.AddError($"阈值下限({Threshold})不能大于阈值上限({MaxValue})");
            }

            if (BlobCountLimit < BlobCountLimitMin || BlobCountLimit > BlobCountLimitMax)
            {
                result.AddError($"查找数量必须在 {BlobCountLimitMin}~{BlobCountLimitMax} 范围内，当前值: {BlobCountLimit}");
            }

            // 预处理参数验证
            if (IsPreprocessingEnabled && PreprocessMethodValue != PreprocessMethod.None)
            {
                int kernelSize = PreprocessKernelSize;
                if (kernelSize < 3 || kernelSize > 31 || kernelSize % 2 == 0)
                {
                    result.AddError($"滤波核大小必须为 3~31 之间的奇数，当前值: {kernelSize}");
                }
            }

            // 形态学参数验证
            if (IsMorphologyEnabled)
            {
                if (MorphElementWidth < 1 || MorphElementWidth > 101)
                {
                    result.AddError($"结构元素宽度必须在 1~101 范围内，当前值: {MorphElementWidth}");
                }
                if (MorphElementHeight < 1 || MorphElementHeight > 101)
                {
                    result.AddError($"结构元素高度必须在 1~101 范围内，当前值: {MorphElementHeight}");
                }
                if (MorphIterations < 1 || MorphIterations > 10)
                {
                    result.AddError($"迭代次数必须在 1~10 范围内，当前值: {MorphIterations}");
                }
            }

            // 结果配置验证
            ValidateFilterRange(IsAreaFilterEnabled, FilterAreaMin, FilterAreaMax, double.MaxValue, "面积筛选");
            ValidateFilterRange(IsCircularityFilterEnabled, FilterCircularityMin, FilterCircularityMax, 1, "圆度筛选");
            ValidateFilterRange(IsRectangularityFilterEnabled, FilterRectangularityMin, FilterRectangularityMax, 1, "矩形度筛选");

            void ValidateFilterRange(bool enabled, double min, double max, double upper, string name)
            {
                if (enabled && (!double.IsFinite(min) || !double.IsFinite(max) || min < 0 || min > max || max > upper))
                    result.AddError($"{name}范围无效，需要有限值且0≤最小值≤最大值≤{upper}");
            }

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
            {
                if (!item.Enabled || item.RuleKind != JudgmentRuleKind.Range) continue;
                if (item.Min < item.AllowedMinimum || item.Max > item.AllowedMaximum)
                    result.AddError($"{item.Name}范围必须在 {item.AllowedMinimum}~{item.AllowedMaximum} 内");
                if (item.Min >= item.Max)
                    result.AddError($"{item.Name}最小值({item.Min})必须小于最大值({item.Max})");
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
