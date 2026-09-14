using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 测量引导参数基类 —— 承载「图像源 + 位置修正 + 测量引导（引导几何 + 卡尺）」三项能力。
    /// </summary>
    /// <remarks>
    /// <para><b>为什么独立成一层</b>（方案 P0.6-4 配套整理）：<b>能力是集合，继承是树</b>。
    /// 此前这三项能力与「区域输入」混装在 <see cref="RoiParameters"/> 一个类里，
    /// 导致**只需要测量引导**的工具（6 个测量工具）被迫继承区域能力
    /// （<c>InspectionRois</c>/<c>MaskRois</c>）——参数类上凭空多出用不到的区域属性、工程文件里也跟着存。</para>
    /// <para>拆层后：</para>
    /// <list type="bullet">
    /// <item>只用 引导/图像源/位置修正 → 继承本类（6 个测量工具）；</item>
    /// <item>还需要区域输入 → 继承 <see cref="RoiParameters"/>（区域工具 + 引导式查找工具）。</item>
    /// </list>
    /// <para><b>与能力声明的关系</b>：本类决定"参数是否<b>物理存在</b>"，
    /// <c>[ToolCapability]</c>（标注在结果类上）决定"框架契约条目是否<b>产出</b>"。二者必须一致。</para>
    /// </remarks>
    [JsonDerivedType(typeof(MeasureGuideParameters), "MeasureGuideParameters")]
    public class MeasureGuideParameters : ToolParameters
    {
        private ImageSourceReference? _imageSourceRef;
        private AvailableDataSource? _imageSource;
        private bool _isPositionCorrectionEnabled;
        private AvailableDataSource? _positionCorrectionBindingSource;
        private ImageSourceReference? _positionCorrectionSource;

        #region 测量引导（引导几何 + 卡尺测量带）

        /// <summary>
        /// 是否包含测量引导几何（引导线 + 卡尺测量带）需要显示到结果层。
        /// 测量查找工具（圆/直线/边缘检测）重写为 true；普通工具默认 false，零影响。
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
        /// 位置修正绑定源显示文本（**展示缓存，不持久化**）。
        /// </summary>
        /// <remarks>
        /// P0.5-F/D：显示名是"结构化路径 + 数据源模型"的**投影**，不再参与持久化 ——
        /// 持久化只认结构化路径（<see cref="PositionCorrectionSource"/> 与绑定源的 <c>BindingSource</c>），
        /// 重开工程时由控件按路径**重算**显示名（<c>ParamBinding.BackfillDisplayTextFromSource</c>）。
        /// 旧工程若已写入该字段，读入时按 <see cref="JsonIgnoreAttribute"/> 忽略，显示不受影响
        /// （避免"改一次命名规则，老工程就显示错名"）。
        /// </remarks>
        [JsonIgnore]
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
    }
}
