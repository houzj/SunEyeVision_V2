using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 几何输入最终区域的【几何来源方式】（= 运行时用哪种解析出唯一区域，见 <see cref="Roi.ResolveRuntime"/>）。
    /// 与 UI 维度对应：<see cref="Drawing"/> = 主模式"绘制"；<see cref="WholeRegionSub"/> 与 <see cref="GeometricSub"/>
    /// = 订阅主模式下的两个子模式（区域订阅 / 参数订阅）。
    /// 关键架构语义（rule: 最终使用的区域几何永远只有一个）：
    /// 一个 <see cref="Roi"/> 的三种配置（<c>LocalGeometry</c>/<c>Binding</c>/<c>ParameterBindings</c>）允许并存保留、
    /// 互不删除；当前激活的解析方式由 <see cref="Roi.Source"/> 显式表达，切换主/子模式只改 <see cref="Roi.Source"/>
    /// 并保留其它配置。仅主模式"订阅→绘制"按用户语义清空全部订阅配置并回到 <see cref="Drawing"/>。
    /// </summary>
    public enum RoiGeometrySource
    {
        /// <summary>本地绘制：使用 <see cref="Roi.LocalGeometry"/>。</summary>
        Drawing = 0,
        /// <summary>整对象订阅：使用 <see cref="Roi.Binding"/> 绑定的上游整个图形。</summary>
        WholeRegionSub = 1,
        /// <summary>参数订阅：使用 <see cref="Roi.ParameterBindings"/> 逐扁平参数拼装几何。</summary>
        GeometricSub = 2,
    }

    /// <summary>
    /// 几何输入项（契约词目 <c>ROI</c> 的载体）：一个 ROI/图形输入。
    /// 一个 Roi = 一个"最终使用的区域槽"；其几何来源方式由 <see cref="Source"/> 显式指定（三选一），
    /// 三种配置（绘制几何 / 整对象订阅 / 参数拼装）允许并存保留、互不删除：
    /// <list type="bullet">
    /// <item><b>本地/绘制（<see cref="RoiGeometrySource.Drawing"/>）</b>：用 <see cref="LocalGeometry"/>；</item>
    /// <item><b>整对象订阅（<see cref="RoiGeometrySource.WholeRegionSub"/>）</b>：<see cref="Binding"/> 携带 <see cref="ParamSetting"/> 契约路径，运行时取上游整个图形；</item>
    /// <item><b>参数订阅（<see cref="RoiGeometrySource.GeometricSub"/>）</b>：<see cref="ParameterBindings"/> 键 = <see cref="RoiShape"/> 扁平参数名，
    ///       形状类型由 <see cref="DeclaredShapeType"/> 决定，运行时按键取 <see cref="ParamSetting"/> 值拼装出几何。</item>
    /// </list>
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
        private RoiGeometrySource _source = RoiGeometrySource.Drawing;
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
        /// 非空表示"参数订阅配置存在"。是否参与运行由 <see cref="Source"/>（<see cref="RoiGeometrySource.GeometricSub"/>）决定。
        /// 与 <see cref="LocalGeometry"/>/<see cref="Binding"/> 允许并存，不互删（配置共存、按 Source 激活）。
        /// </summary>
        public Dictionary<string, ParamSetting>? ParameterBindings
        {
            get => _parameterBindings;
            set => SetProperty(ref _parameterBindings, value);
        }

        /// <summary>
        /// 当前【激活】的几何来源方式（最终使用的区域几何只由它解析，三选一）。持久化字段。
        /// 子模式切换/配置绑定会更新本字段；其它配置仍保留（供切回时直接激活恢复）。
        /// </summary>
        public RoiGeometrySource Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
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

        /// <summary>是否激活整对象订阅（Source == WholeRegionSub）。配置是否齐全见 <see cref="Binding"/>。</summary>
        [JsonIgnore]
        public bool IsSubscribed => Source == RoiGeometrySource.WholeRegionSub;

        /// <summary>是否激活参数拼装（Source == GeometricSub）。配置是否齐全见 <see cref="ParameterBindings"/>。</summary>
        [JsonIgnore]
        public bool IsComposed => Source == RoiGeometrySource.GeometricSub;

        /// <summary>是否本地/绘制（Source == Drawing）。</summary>
        [JsonIgnore]
        public bool IsLocal => Source == RoiGeometrySource.Drawing;

        /// <summary>
        /// 创建绘制模式的几何输入
        /// </summary>
        public static Roi CreateLocal(string name, object geometry)
        {
            return new Roi
            {
                Name = name,
                Source = RoiGeometrySource.Drawing,
                LocalGeometry = new RoiShape { Geometry = geometry },
            };
        }

        /// <summary>
        /// 创建整对象订阅模式的几何输入
        /// </summary>
        public static Roi CreateSubscribed(string name, string sourceNodeId, string sourceProperty)
        {
            return new Roi
            {
                Name = name,
                Source = RoiGeometrySource.WholeRegionSub,
                Binding = ParamSetting.CreateBinding(name, sourceNodeId, sourceProperty),
            };
        }

        /// <summary>
        /// 创建参数订阅模式的几何输入（键 = RoiShape 扁平参数名）
        /// </summary>
        public static Roi CreateComposed(string name, ShapeType shapeType,
            IEnumerable<KeyValuePair<string, ParamSetting>>? parameterBindings = null)
        {
            return new Roi
            {
                Name = name,
                Source = RoiGeometrySource.GeometricSub,
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
                Source = Source,
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
            if (Source == RoiGeometrySource.GeometricSub)
            {
                var binds = string.Join(", ", ParameterBindings!.Select(kv => $"{kv.Key}→{kv.Value}"));
                return $"{Name} (拼装[{DeclaredShapeType}]: {binds})";
            }
            return Source == RoiGeometrySource.WholeRegionSub
                ? $"{Name} (订阅: {Binding?.ToString() ?? "未配置"})"
                : $"{Name} (绘制: {LocalGeometry?.ToString() ?? "空"})";
        }

        /// <summary>
        /// 统一运行时解析出口：把 Roi 声明（本地绘制 / 整对象订阅 / 参数拼装）解析为实际几何列表（RoiShape[]）。
        /// 算法适配层、结果契约输出、结果层绘制等所有消费方必须经此出口取值，禁止直读 <see cref="LocalGeometry"/>。
        /// <list type="bullet">
        /// <item>本地：返回 [LocalGeometry]（无本地几何返回空）；</item>
        /// <item>整对象订阅：经 <see cref="IParameterContext"/> 取上游 RoiShape[]/RoiShape/Rect，未提供上下文直接抛异常（rule-015 报错优先）；</item>
        /// <item>参数拼装：按 <see cref="DeclaredShapeType"/> 模板 + 逐扁平参数经上下文解析合成一个 RoiShape。</item>
        /// </list>
        /// </summary>
        public IReadOnlyList<RoiShape> ResolveRuntime(IParameterContext? context)
        {
            // 按当前激活来源（Source）三选一解析——最终使用的区域几何永远只有一个。
            switch (Source)
            {
                case RoiGeometrySource.WholeRegionSub:
                    if (context == null)
                        throw new InvalidOperationException(
                            $"无法解析整对象订阅区域 '{Name}': 未提供节点结果上下文（IParameterContext）");
                    if (Binding == null)
                        throw new InvalidOperationException(
                            $"整对象订阅区域 '{Name}' 未配置 Binding（Source=WholeRegionSub 但绑定为空，请到订阅面板重新绑定）");
                    return ResolveSubscription(context);

                case RoiGeometrySource.GeometricSub:
                {
                    if (context == null)
                        throw new InvalidOperationException(
                            $"无法解析参数订阅区域 '{Name}': 未提供节点结果上下文（IParameterContext）");

                    // 向量化多实例拼装（"有几个解析几个"）：逐扁平参数取原始值（标量广播 / double[] 数组按颗），
                    // 由 RoiComposeResolver 按数组长度 N 合成 N 颗几何；1 颗与 N 颗走同一条批量路径。
                    var rawValues = new Dictionary<string, object?>();
                    if (ParameterBindings != null)
                    {
                        foreach (var kv in ParameterBindings)
                        {
                            rawValues[kv.Key] = ResolveFlatParameterRaw(context, kv.Key);
                        }
                    }

                    var geometries = RoiComposeResolver.ComposeAllByTemplate(DeclaredShapeType, rawValues);
                    return geometries
                        .Where(g => g != null)
                        .Select(g => new RoiShape { Geometry = g })
                        .ToArray();
                }

                default: // Drawing
                    return LocalGeometry is { Geometry: not null } local
                        ? new[] { local }
                        : Array.Empty<RoiShape>();
            }
        }

        /// <summary>整对象订阅：经上下文按契约路径取上游几何（RoiShape[]/RoiShape/Rect）并规范化。</summary>
        private IReadOnlyList<RoiShape> ResolveSubscription(IParameterContext context)
        {
            object? value;
            try
            {
                value = context.GetNodeOutputValue(Binding!.SourceNodeId!, Binding.SourceProperty!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"解析整对象订阅区域失败: 节点 {Binding.SourceNodeId}.{Binding.SourceProperty}: {ex.Message}", ex);
            }

            return value switch
            {
                RoiShape[] shapes => shapes.Where(s => s?.Geometry != null).ToArray(),
                RoiShape shape when shape?.Geometry != null => new[] { shape },
                System.Windows.Rect rect => new[] { new RoiShape { Geometry = rect } },
                null => Array.Empty<RoiShape>(),
                _ => throw new NotSupportedException(
                    $"整对象订阅 '{Name}' 的解析值类型 {value.GetType().Name} 不支持（仅支持 RoiShape[]/RoiShape/Rect）"),
            };
        }

        /// <summary>
        /// 参数拼装单键取**原始值**（向量化拼装用）：常量 → ConstantValue；
        /// 绑定 → 经上下文按契约路径解析（可为标量 double，或字段叶无索引绑定解析出的
        /// <c>double[]</c> = 该类型全部图形该字段）；未知/失败 → null（走模板默认）。
        /// 数值收敛与逐颗取值由 <see cref="RoiComposeResolver.ToScalar"/> 统一处理（不在此收敛，防数组被吞）。
        /// </summary>
        private object? ResolveFlatParameterRaw(IParameterContext context, string parameterName)
        {
            if (ParameterBindings == null || !ParameterBindings.TryGetValue(parameterName, out var setting) || setting == null)
                return null;

            switch (setting.BindingType)
            {
                case BindingType.Constant:
                    return setting.ConstantValue;

                case BindingType.Binding:
                    if (string.IsNullOrEmpty(setting.SourceNodeId) || string.IsNullOrEmpty(setting.SourceProperty))
                        return null;
                    try
                    {
                        return context.GetNodeOutputValue(setting.SourceNodeId!, setting.SourceProperty!);
                    }
                    catch
                    {
                        return null;
                    }

                default:
                    return null;
            }
        }
    }
}
