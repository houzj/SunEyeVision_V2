using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 区域输入参数基类 - 在 <see cref="MeasureGuideParameters"/> 之上增加「检测区 / 屏蔽区」区域能力。
    /// </summary>
    /// <remarks>
    /// 设计理念：
    /// 1. 需要区域管理的工具继承此基类
    /// 2. 避免重复定义 InspectionRois 和 MaskRois
    /// 3. 统一几何输入参数的序列化和反序列化
    ///
    /// ★ 承载层拆分（方案 P0.6-4）：图像源 / 位置修正 / 测量引导 已上移到基类
    ///   <see cref="MeasureGuideParameters"/>，本类**只留区域**。
    ///   类名与 JSON 判别符 <c>"RoiParameters"</c>、字段名 <c>inspectionRois</c>/<c>maskRois</c>
    ///   <b>均保持不变</b> —— 老工程零迁移；不过区域的测量工具改继承基类后，其参数类上不再物理存在区域属性。
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
    ///         Threshold = new ParamValue&lt;int&gt;(128);
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [JsonDerivedType(typeof(RoiParameters), "RoiParameters")]
    public class RoiParameters : MeasureGuideParameters
    {
        private RoiCollection _inspectionRois;
        private RoiCollection _maskRois;

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

        /// <summary>
        /// 按角色获取运行时有效几何输入列表（执行层/结果层统一出口，语义=按模式过滤）：
        /// 组内存在订阅/拼装 Roi 时仅返回订阅/拼装 Roi（本地绘制底稿不参与）；全本地时返回全部启用输入。
        /// </summary>
        /// <param name="mode">几何输入角色（检测/屏蔽）</param>
        /// <returns>指定角色的运行时有效几何输入集合</returns>
        public System.Collections.Generic.IEnumerable<Roi> GetRoisByType(RoiParameterMode mode)
        {
            return mode == RoiParameterMode.InspectionRoi
                ? _inspectionRois.GetRuntimeActiveInputs()
                : _maskRois.GetRuntimeActiveInputs();
        }
    }
}
