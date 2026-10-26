using System;
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
    /// 阈值极性枚举（与分割方式独立）
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

    public enum BlobSegmentationMode { SingleThreshold, DoubleThreshold, None }

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
    public class BlobAnalysisParameters : RoiParameters, IInputSlotContract, IRuntimeParameterContract, IOutputContractProvider, IStoredConfigurationValidator, IBindingInputActivityProvider, IConfigurationFieldProvider
    {
        public BlobAnalysisParameters()
        {
            var defaults=new Runtime.BlobSettings();
            Threshold=defaults.Threshold;
            UpperThreshold=defaults.UpperThreshold;
            SegmentationMode=(BlobSegmentationMode)defaults.SegmentationMode;
            Type=(ThresholdType)defaults.Type;
            IsPositionCorrectionEnabled=defaults.IsPositionCorrectionEnabled;
            IsAreaFilterEnabled=defaults.IsAreaFilterEnabled;
            FilterAreaMin=defaults.FilterAreaMin;
            FilterAreaMax=defaults.FilterAreaMax;
            IsCircularityFilterEnabled=defaults.IsCircularityFilterEnabled;
            FilterCircularityMin=defaults.FilterCircularityMin;
            FilterCircularityMax=defaults.FilterCircularityMax;
            IsRectangularityFilterEnabled=defaults.IsRectangularityFilterEnabled;
            FilterRectangularityMin=defaults.FilterRectangularityMin;
            FilterRectangularityMax=defaults.FilterRectangularityMax;
            IsBlobCountFilterEnabled=defaults.IsBlobCountFilterEnabled;
            BlobCountLimit=defaults.BlobCountLimit;
            OutputBlobImage=defaults.OutputGrayImage;
            OutputBinaryImage=defaults.OutputBinaryImage;
            OutputContours=defaults.OutputBoundary;
            SortMode=(BlobSortMode)defaults.SortMode;
        }

        public SunEyeVision.Plugin.Contracts.RegionFieldMode InspectionMode { get; set; }
        public SunEyeVision.Plugin.Contracts.RegionFieldMode MaskMode { get; set; }
        [JsonIgnore] public SunEyeVision.Plugin.Contracts.IReadOnlyRegion? InspectionRegionInput { get; set; }
        [JsonIgnore] public SunEyeVision.Plugin.Contracts.IReadOnlyRegion? MaskRegionInput { get; set; }
        public bool IsBindingInputActive(string key)
        {
            if(key==nameof(Threshold)||key==nameof(Type))return SegmentationMode!=BlobSegmentationMode.None;
            if(key==nameof(UpperThreshold))return SegmentationMode==BlobSegmentationMode.DoubleThreshold;
            if(key==nameof(InspectionRegionInput))return InspectionMode==SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRegion;
            if(key==nameof(MaskRegionInput))return MaskMode==SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRegion;
            return ActiveGeometry(key,nameof(InspectionRois),InspectionRois,InspectionMode)
                ??ActiveGeometry(key,nameof(MaskRois),MaskRois,MaskMode)??true;
        }
        private static bool? ActiveGeometry(string key,string name,RoiCollection collection,SunEyeVision.Plugin.Contracts.RegionFieldMode mode)
        {
            if(key==name)return mode!=SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRegion;
            if(!key.StartsWith(name+":",System.StringComparison.Ordinal))return null;
            if(mode==SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRegion)return false;
            var roi=collection.Rois.FirstOrDefault(r=>key==name+":"+r.Id||key.StartsWith(name+":"+r.Id+":",System.StringComparison.Ordinal));
            if(roi is null||!ActiveRoi(roi,mode))return false;
            if(roi.IsComposed&&key.StartsWith(name+":"+roi.Id+":",StringComparison.Ordinal))
                return RoiComposeResolver.RequiredParameters(roi.DeclaredShapeType).Contains(key[(name+":"+roi.Id+":").Length..],StringComparer.Ordinal);
            return true;
        }
        private static bool ActiveRoi(Roi roi,SunEyeVision.Plugin.Contracts.RegionFieldMode mode)=>mode switch
        {
            SunEyeVision.Plugin.Contracts.RegionFieldMode.LocalGeometry=>roi.IsLocal,
            SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedGeometry=>!roi.IsLocal,
            SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRectangle=>roi.IsSubscribed&&roi.InheritedShapeType==ShapeType.RotatedRectangle,
            SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedCircle=>roi.IsSubscribed&&roi.InheritedShapeType==ShapeType.Circle,
            SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedRectangleParameters=>roi.IsComposed&&roi.DeclaredShapeType==ShapeType.RotatedRectangle,
            SunEyeVision.Plugin.Contracts.RegionFieldMode.InheritedCircleParameters=>roi.IsComposed&&roi.DeclaredShapeType==ShapeType.Circle,
            _=>false
        };
        public IEnumerable<Roi> GetActiveRois(bool mask=false)=>(mask?MaskRois:InspectionRois).Rois.Where(roi=>roi.IsEnabled&&ActiveRoi(roi,mask?MaskMode:InspectionMode));
        public IReadOnlyList<SunEyeVision.Plugin.SDK.Execution.Results.OutputVariable> GetOutputContract()=>BlobOutputContract.Describe();
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
                ValueType: typeof(RoiCollection), Required: false),
            new InputSlotDescriptor(nameof(InspectionRegionInput), "检测像素区域", InputSlotKind.Roi,
                ValueType: typeof(SunEyeVision.Plugin.Contracts.IReadOnlyRegion), Required: false),
            new InputSlotDescriptor(nameof(MaskRegionInput), "屏蔽像素区域", InputSlotKind.Roi,
                ValueType: typeof(SunEyeVision.Plugin.Contracts.IReadOnlyRegion), Required: false)
        };

        public IReadOnlyList<ConfigurationFieldAccessor> GetConfigurationFields()=>BlobConfigurationFields.Describe(this);
        SunEyeVision.Plugin.Contracts.ToolSchema? IConfigurationFieldProvider.ConfigurationSchema=>BlobInputContract.Describe();
        IReadOnlyDictionary<string,string> IConfigurationFieldProvider.RegionInputStorage=>new Dictionary<string,string>
        {{"inspection",nameof(InspectionRegionInput)},{"mask",nameof(MaskRegionInput)}};

        public IReadOnlyList<RuntimeParameterDescriptor> GetRuntimeParameters() => new[]
        {
            new RuntimeParameterDescriptor(nameof(Threshold), "阈值", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(UpperThreshold), "阈值上限", "Integer", typeof(int)),
            new RuntimeParameterDescriptor(nameof(Type), "极性", ValueType: typeof(ThresholdType)),
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
        public int Threshold { get; set; }

        /// <summary>
        /// 双阈值选取区间上限(0-255)
        /// </summary>
        [DisplayName("阈值上限")]
        public int UpperThreshold { get; set; }

        [DisplayName("分割方式")]
        public BlobSegmentationMode SegmentationMode { get; set; }

        /// <summary>
        /// 阈值极性（Binary为区间内，BinaryInv为有效域内补集）
        /// </summary>
        [DisplayName("极性")]
        public ThresholdType Type { get; set; }

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
        public bool IsBlobCountFilterEnabled { get; set; }

        [DisplayName("查找数量")]
        public int BlobCountLimit { get; set; }

        [DisplayName("Blob图像输出")]
        public bool OutputBlobImage { get; set; }

        [DisplayName("二值化图像输出")]
        public bool OutputBinaryImage { get; set; }

        [DisplayName("轮廓输出")]
        public bool OutputContours { get; set; }

        public const int BlobCountLimitMin = 1;
        public const int BlobCountLimitMax = 20000;

        private static ResultPresentationConfig CreateResultPresentation()
        {
            var config = new ResultPresentationConfig();
            foreach(var item in Runtime.BlobParameterDefinition.Instance.JudgmentMetadata)
            {
                string Get(string key)=>(string?)item.Attribute(key)??throw new InvalidOperationException("判定元数据缺失："+key);
                double? Number(string key)=>item.Attribute(key) is {} value?double.Parse(value.Value,System.Globalization.CultureInfo.InvariantCulture):null;
                config.Judgments.Add(new(){Key=Get("key"),Name=Get("name"),RuleKind=Enum.Parse<JudgmentRuleKind>(Get("kind")),
                    Scope=bool.Parse(Get("perItem"))?JudgmentScope.PerItem:JudgmentScope.Overall,
                    AffectsItemDisplayState=bool.Parse(Get("perItem")),Enabled=bool.Parse(Get("enabled")),
                    Min=Number("minimum")!.Value,Max=Number("maximum")!.Value,AllowedMinimum=Number("allowedMinimum")!.Value,AllowedMaximum=Number("allowedMaximum")!.Value,
                    Unit=Get("unit"),DecimalPlaces=int.Parse(Get("decimals"),System.Globalization.CultureInfo.InvariantCulture),Order=int.Parse(Get("order"),System.Globalization.CultureInfo.InvariantCulture)});
            }
            static Color ColorValue(uint argb)=>Color.FromArgb((byte)(argb>>24),(byte)(argb>>16),(byte)(argb>>8),(byte)argb);
            var display=Runtime.BlobDisplayDefinition.Instance;
            foreach(var item in display.Graphics)
                config.Graphics.Add(new(){Key=item.Key,Name=item.Label,Order=item.Order,Style=new(){Name=item.StyleName,
                    IsVisible=item.Visible,OkColor=ColorValue(item.OkColor),NgColor=ColorValue(item.NgColor),
                    Opacity=item.Opacity,LineWidth=item.LineWidth,CrossSize=item.CrossSize}});
            foreach(var item in display.Texts)
                config.Texts.Add(new(){Key=item.Key,Name=item.Label,Order=item.Order,Style=new(){Name=item.StyleName,
                    IsVisible=item.Visible,OkColor=ColorValue(item.OkColor),NgColor=ColorValue(item.NgColor),Opacity=item.Opacity,
                    FontSize=item.FontSize,IsBold=item.Bold,PositionX=item.X,PositionY=item.Y}});
            return config;
        }

        #region 排序方式

        /// <summary>
        /// Blob排序方式
        /// </summary>
        [DisplayName("排序方式")]
        public BlobSortMode SortMode { get; set; }

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

        private double _filterAreaMin;
        private double _filterAreaMax;
        private double _filterCircularityMin;
        private double _filterCircularityMax;
        private double _filterRectangularityMin;
        private double _filterRectangularityMax;
        private bool _isAreaFilterEnabled;
        private bool _isCircularityFilterEnabled;
        private bool _isRectangularityFilterEnabled;

        #endregion

        /// <summary>
        /// 验证参数
        /// </summary>
        public override ValidationResult Validate()
        {
            var result = base.Validate();
            foreach(var issue in ValidateStoredConfiguration())result.AddError(issue.Message);

            if (!System.Enum.IsDefined(SegmentationMode) || !System.Enum.IsDefined(Type)) result.AddError("分割方式或极性无效。");
            if (SegmentationMode != BlobSegmentationMode.None && (Threshold < 0 || Threshold > 255)) result.AddError("阈值必须在0至255之间。");
            if (SegmentationMode == BlobSegmentationMode.DoubleThreshold && (UpperThreshold < Threshold || UpperThreshold > 255)) result.AddError("双阈值必须满足0≤下限≤上限≤255。");

            if (BlobCountLimit < BlobCountLimitMin || BlobCountLimit > BlobCountLimitMax)
            {
                result.AddError($"查找数量必须在 {BlobCountLimitMin}~{BlobCountLimitMax} 范围内，当前值: {BlobCountLimit}");
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

        public IReadOnlyList<SunEyeVision.Plugin.Contracts.Diagnostic> ValidateStoredConfiguration(IReadOnlyCollection<string>? boundParameterNames=null)
            =>BlobInputContract.ValidateStoredConfiguration(this,boundParameterNames);

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

    }
}
