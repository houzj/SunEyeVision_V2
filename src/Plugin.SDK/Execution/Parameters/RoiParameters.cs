using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入参数基类 - 提供检测区与屏蔽区几何输入（Roi）的通用实现
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// 1. 所有需要区域管理的工具可以继承此基类
    /// 2. 避免重复定义 InspectionRois 和 MaskRois
    /// 3. 统一几何输入参数的序列化和反序列化
    /// 
    /// 使用示例：
    /// <code>
    /// [JsonDerivedType(typeof(ThresholdParameters), "Threshold")]
    /// public class ThresholdParameters : RoiParameters
    /// {
    ///     public ParamValue<int> Threshold { get; set; }
    ///     
    ///     public ThresholdParameters()
    ///     {
    ///         Threshold = new ParamValue<int>(128);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [JsonDerivedType(typeof(RoiParameters), "RoiParameters")]
    public class RoiParameters : ToolParameters
    {
        private RoiCollection _inspectionRois;
        private RoiCollection _maskRois;
        private ImageSourceReference? _imageSourceRef;
        private AvailableDataSource? _imageSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        public RoiParameters()
        {
            _inspectionRois = new RoiCollection(RoiParameterMode.InspectionRoi);
            _maskRois = new RoiCollection(RoiParameterMode.MaskRoi);
        }

        /// <summary>
        /// 检测几何输入集合 - 指定需要进行检测/处理的区域（元素为 Roi 几何输入）
        /// </summary>
        /// <remarks>
        /// 支持常量模式（用户直接绘制）和绑定模式（订阅其他节点输出）
        /// </remarks>
        [JsonPropertyOrder(10)]
        public RoiCollection InspectionRois
        {
            get => _inspectionRois;
            set => _inspectionRois = value;
        }

        /// <summary>
        /// 屏蔽几何输入集合 - 指定需要排除的区域（元素为 Roi 几何输入）
        /// </summary>
        /// <remarks>
        /// 支持常量模式（用户直接绘制）和绑定模式（订阅其他节点输出）
        /// </remarks>
        /// <summary>
        /// 是否包含测量引导几何（引导线 + 卡尺测量带）需要显示到结果层。
        /// 测量查找工具（圆/直线/边缘检测）重写为 true；普通区域工具默认 false，零影响。
        /// </summary>
        public virtual bool HasMeasureGuide => false;

        /// <summary>引导线显示样式（Tab3 可控制；测量工具重写提供，默认 null）</summary>
        public virtual GraphicsStyle? GuideStyle { get; set; }

        /// <summary>卡尺测量带显示样式（Tab3 可控制；测量工具重写提供，默认 null）</summary>
        public virtual GraphicsStyle? CaliberStyle { get; set; }

        /// <summary>
        /// 构建引导线 + 卡尺测量带的显示项（与检测/屏蔽区域同通道走结果层）。
        /// 测量工具重写返回引导几何与测量带；默认返回空。
        /// </summary>
        /// <param name="correctionMatrix">位置修正矩阵（参考坐标→当前图像坐标；null=不修正）</param>
        public virtual System.Collections.Generic.IEnumerable<DisplayItemBase> BuildMeasureGuideItems(
            AffineMatrix? correctionMatrix)
            => System.Array.Empty<DisplayItemBase>();
        [JsonPropertyOrder(11)]
        public RoiCollection MaskRois
        {
            get => _maskRois;
            set => _maskRois = value;
        }

        #region 显示样式

        /// <summary>检测区显示样式（UI 可通过 DispItemColorControl 绑定控制可见性）</summary>
        [JsonIgnore]
        public virtual GraphicsStyle InspectionRoiStyle { get; set; } = new()
        {
            Name = "InspectionRoi",
            IsVisible = true,
            OkColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            LineWidth = 1.5
        };

        /// <summary>屏蔽区显示样式（UI 可通过 DispItemColorControl 绑定控制可见性）</summary>
        [JsonIgnore]
        public virtual GraphicsStyle MaskRoiStyle { get; set; } = new()
        {
            Name = "MaskRoi",
            IsVisible = true,
            OkColor = System.Windows.Media.Color.FromRgb(255, 165, 0),  // 橙色
            LineWidth = 1.5
        };

        #endregion

        #region 图像源绑定

        /// <summary>
        /// 可序列化的图像源引用
        /// </summary>
        /// <remarks>
        /// 存储 ImageSource 的身份+展示信息（4个字符串），用于 JSON 序列化/反序列化。
        /// 运行时 ImageSource 从此引用通过 RestoreImageSource() 恢复。
        /// </remarks>
        [JsonPropertyOrder(5)]
        [JsonPropertyName("imageSourceRef")]
        public ImageSourceReference? ImageSourceRef
        {
            get => _imageSourceRef;
            set => _imageSourceRef = value;
        }

        /// <summary>
        /// 输入图像源（运行时，不序列化，从 ImageSourceRef 恢复）
        /// </summary>
        /// <remarks>
        /// AvailableDataSource 包含运行时数据（Mat 图像、TreeNode 等）无法序列化，
        /// 因此标记 [JsonIgnore] 不参与序列化。
        /// setter 中自动同步更新 ImageSourceRef。
        /// </remarks>
        [JsonIgnore]
        public AvailableDataSource? ImageSource
        {
            get => _imageSource;
            set
            {
                if (SetProperty(ref _imageSource, value, "输入图像"))
                {
                    // 自动同步到可序列化的引用
                    _imageSourceRef = ImageSourceReference.FromDataSource(value);
                }
            }
        }

        /// <summary>
        /// 从 AvailableDataSources 列表恢复 ImageSource
        /// </summary>
        /// <param name="dataSources">当前可用的数据源列表</param>
        /// <returns>是否成功恢复</returns>
        public bool RestoreImageSource(System.Collections.Generic.IEnumerable<AvailableDataSource>? dataSources)
        {
            if (dataSources == null)
                return false;

            // 分支1：有序列化引用 → 精确匹配恢复
            if (_imageSourceRef != null)
            {
                var matched = _imageSourceRef.MatchDataSource(dataSources);
                if (matched != null)
                {
                    // 直接设置字段，避免触发 setter 中的重复同步
                    _imageSource = matched;
                    OnPropertyChanged(nameof(ImageSource));
                    return true;
                }
                return false;
            }

            // 分支2：无序列化引用且无运行时图像源 → 数据驱动自动匹配
            // 通过 AvailableDataSource.VariableType == "Image" 匹配（统一 Mapper 词汇体系）
            if (_imageSource == null)
            {
                var firstImageSource = dataSources
                    .FirstOrDefault(ds => ds.VariableType == "Image");
                if (firstImageSource != null)
                {
                    _imageSource = firstImageSource;
                    _imageSourceRef = ImageSourceReference.FromDataSource(firstImageSource);
                    OnPropertyChanged(nameof(ImageSource));
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 位置修正（算法工具标配）

        private bool _isPositionCorrectionEnabled;
        private AvailableDataSource? _positionCorrectionBindingSource;
        private ImageSourceReference? _positionCorrectionSource;

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
        /// 按角色获取启用的几何输入列表
        /// </summary>
        /// <param name="mode">几何输入角色（检测/屏蔽）</param>
        /// <returns>指定角色的启用几何输入集合</returns>
        public System.Collections.Generic.IEnumerable<Roi> GetRoisByType(RoiParameterMode mode)
        {
            return mode == RoiParameterMode.InspectionRoi
                ? _inspectionRois.GetEnabledInputs()
                : _maskRois.GetEnabledInputs();
        }

    }
}
