using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入项（契约词目 <c>ROI</c> 的载体）：一个 ROI/图形输入。
    /// 三态互斥模型，与 <see cref="ParamValue{T}"/> 的"常量/绑定"同构：
    /// <list type="bullet">
    /// <item><b>本地/绘制</b>：<see cref="LocalGeometry"/> 非空，<see cref="Binding"/> 与 <see cref="ParameterBindings"/> 为 null；</item>
    /// <item><b>整对象订阅</b>：<see cref="LocalGeometry"/> 为 null，<see cref="Binding"/> 携带 <see cref="ParamSetting"/> 契约路径；</item>
    /// <item><b>参数拼装</b>：<see cref="ParameterBindings"/> 非空，键 = <see cref="RoiShape"/> 扁平参数名（CenterX/CenterY/Width/Height/Radius/Angle/StartX/...），
    ///       形状类型由 <see cref="DeclaredShapeType"/> 决定，运行时按键取 <see cref="ParamSetting"/> 值拼装出几何。</item>
    /// </list>
    /// 三态互斥由编辑器 VM 切换维护（切一种清另外两种），模型层只承载状态。
    /// 语义定位：取代历史 <c>RoiData</c>（UI.Controls.Roi.Models）的几何输入职责，订阅语义统一为 <see cref="ParamSetting"/>。
    /// </summary>
    public class Roi : ObservableObject, ICloneable
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private RoiShape? _localGeometry;
        private ParamSetting? _binding;
        private Dictionary<string, ParamSetting>? _parameterBindings;
        private ShapeType _declaredShapeType = ShapeType.None;
        private ShapeType _composeShapeType = ShapeType.None;
        private bool _isEnabled = true;
        private bool _isVisible = true;
        private bool _isEditable = true;

        /// <summary>唯一标识符</summary>
        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>几何输入名称</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, "几何输入名称");
        }

        /// <summary>
        /// 本地几何（绘制）；订阅模式下可为 null。
        /// </summary>
        public RoiShape? LocalGeometry
        {
            get => _localGeometry;
            set => SetProperty(ref _localGeometry, value);
        }

        /// <summary>
        /// 订阅绑定（null=本地/绘制；非 null=订阅）。
        /// 整对象订阅 / 图形参数订阅仅为 <see cref="ParamSetting.SourceProperty"/> 契约路径深度差异。
        /// </summary>
        public ParamSetting? Binding
        {
            get => _binding;
            set => SetProperty(ref _binding, value);
        }

        /// <summary>
        /// 参数拼装绑定集（键 = <see cref="RoiShape"/> 扁平参数名 CenterX/CenterY/Width/Height/Radius/Angle/StartX/...）。
        /// 非空 = 参数拼装模式：运行时按 <see cref="DeclaredShapeType"/> 决定形状模板，逐键取 <see cref="ParamSetting"/> 值拼装几何。
        /// 与 <see cref="LocalGeometry"/>/<see cref="Binding"/> 三态互斥（由编辑器 VM 维护）。
        /// </summary>
        public Dictionary<string, ParamSetting>? ParameterBindings
        {
            get => _parameterBindings;
            set => SetProperty(ref _parameterBindings, value);
        }

        /// <summary>
        /// 声明形状类型（订阅模式下表达期望图形类型；绘制模式由 LocalGeometry.ShapeType 推导）。
        /// 参数拼装模式下承载目标形状（决定扁平参数键集与几何模板）。
        /// 本属性是派生视图：仅当编辑器显式声明时持久化 <see cref="ComposeShapeType"/>；
        /// 整对象订阅/绘制形状不在此持久化（分别由绑定路径 [Type=X]/Geometry 表达）。
        /// </summary>
        [JsonIgnore]
        public ShapeType DeclaredShapeType
        {
            get => IsComposed
                ? (ComposeShapeType != ShapeType.None ? ComposeShapeType : _declaredShapeType)
                : _declaredShapeType != ShapeType.None
                    ? _declaredShapeType
                    : (LocalGeometry?.ShapeType ?? ShapeType.None);
            set => SetProperty(ref _declaredShapeType, value);
        }

        /// <summary>
        /// 参数拼装目标形状（持久化字段，序列化键 <c>composeShapeType</c>）。
        /// 仅参数拼装模式使用；绘制/整对象订阅模式下保持 None。
        /// </summary>
        public ShapeType ComposeShapeType
        {
            get => _composeShapeType;
            set => SetProperty(ref _composeShapeType, value);
        }

        /// <summary>是否启用</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value, "启用状态");
        }

        /// <summary>是否可见</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value, "可见性");
        }

        /// <summary>是否可编辑</summary>
        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value, "可编辑性");
        }

        /// <summary>是否为整对象订阅模式</summary>
        [JsonIgnore]
        public bool IsSubscribed => Binding != null;

        /// <summary>是否为参数拼装模式（ParameterBindings 非空）</summary>
        [JsonIgnore]
        public bool IsComposed => ParameterBindings is { Count: > 0 };

        /// <summary>是否为本地/绘制模式（非整对象订阅 且 非参数拼装）</summary>
        [JsonIgnore]
        public bool IsLocal => !IsSubscribed && !IsComposed;

        /// <summary>
        /// 创建绘制模式的几何输入
        /// </summary>
        public static Roi CreateLocal(string name, object geometry)
        {
            return new Roi
            {
                Name = name,
                LocalGeometry = new RoiShape { Geometry = geometry },
            };
        }

        /// <summary>
        /// 创建订阅模式的几何输入
        /// </summary>
        public static Roi CreateSubscribed(string name, string sourceNodeId, string sourceProperty)
        {
            return new Roi
            {
                Name = name,
                Binding = ParamSetting.CreateBinding(name, sourceNodeId, sourceProperty),
            };
        }

        /// <summary>
        /// 创建参数拼装模式的几何输入（键 = RoiShape 扁平参数名）
        /// </summary>
        public static Roi CreateComposed(string name, ShapeType shapeType,
            IEnumerable<KeyValuePair<string, ParamSetting>>? parameterBindings = null)
        {
            return new Roi
            {
                Name = name,
                ComposeShapeType = shapeType,
                ParameterBindings = parameterBindings != null
                    ? new Dictionary<string, ParamSetting>(parameterBindings)
                    : new Dictionary<string, ParamSetting>(),
            };
        }

        /// <summary>克隆</summary>
        public object Clone()
        {
            return new Roi
            {
                Id = Id,
                Name = Name,
                LocalGeometry = LocalGeometry?.Clone(),
                Binding = Binding?.Clone(),
                ParameterBindings = ParameterBindings != null
                    ? ParameterBindings.ToDictionary(kv => kv.Key, kv => kv.Value.Clone())
                    : null,
                DeclaredShapeType = _declaredShapeType,
                ComposeShapeType = ComposeShapeType,
                IsEnabled = IsEnabled,
                IsVisible = IsVisible,
                IsEditable = IsEditable,
            };
        }

        public override string ToString()
        {
            if (IsComposed)
            {
                var binds = string.Join(", ", ParameterBindings!.Select(kv => $"{kv.Key}→{kv.Value}"));
                return $"{Name} (拼装[{DeclaredShapeType}]: {binds})";
            }
            return IsSubscribed
                ? $"{Name} (订阅: {Binding!.ToString()})"
                : $"{Name} (绘制: {LocalGeometry?.ToString() ?? "空"})";
        }
    }
}
