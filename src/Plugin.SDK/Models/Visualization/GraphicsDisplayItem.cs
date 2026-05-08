using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 渲染模式：决定逐元素通过状态如何驱动颜色
    /// </summary>
    public enum DisplayRenderMode
    {
        /// <summary>独立模式：逐元素使用 IsPassArray 决定 OK/NG 颜色（用于 Blob 分析等）</summary>
        Individual,
        /// <summary>统一模式：全部使用 Style.OkColor（用于模板匹配等）</summary>
        Unified
    }

    /// <summary>
    /// 图形显示项 - 用于渲染点/线/圆/矩形等几何图形
    /// 样式由 Style 引用提供（与 Parameters/UI 共享同一实例）
    /// Style 变更时自动同步 IsVisible 并触发重绘
    /// </summary>
    public sealed class GraphicsDisplayItem : DisplayItemBase
    {
        private bool _isFilled;
        private GraphicsStyle? _style;

        /// <summary>图形样式引用（与UI控件、Parameters共享同一实例）</summary>
        [JsonIgnore]
        public GraphicsStyle? Style
        {
            get => _style;
            set
            {
                if (_style != null)
                    _style.PropertyChanged -= OnStylePropertyChanged;

                _style = value;

                if (_style != null)
                {
                    _style.PropertyChanged += OnStylePropertyChanged;
                    IsVisible = _style.IsVisible;
                }
            }
        }

        /// <summary>图形类型（运行时，不序列化）</summary>
        [JsonIgnore]
        public GraphicsType Type { get; set; }

        /// <summary>几何数据数组（运行时，不序列化）</summary>
        [JsonIgnore]
        public object[] Geometries { get; set; } = Array.Empty<object>();

        /// <summary>逐元素通过标记（与 Geometries 一一对应，空数组表示全通过）</summary>
        [JsonIgnore]
        public bool[] IsPassArray { get; set; } = Array.Empty<bool>();

        /// <summary>是否填充（渲染提示，由工具开发者按需设置）</summary>
        public bool IsFilled { get => _isFilled; set => SetProperty(ref _isFilled, value); }

        /// <summary>渲染模式：独立颜色(Individual)或统一颜色(Unified)</summary>
        public DisplayRenderMode RenderMode { get; set; } = DisplayRenderMode.Individual;

        /// <summary>临时几何数据列表（构造期间积累，Freeze 后转为数组）</summary>
        [JsonIgnore]
        internal List<object> GeometryList { get; } = new();

        /// <summary>临时通过标记列表（构造期间积累，Freeze 后转为数组）</summary>
        [JsonIgnore]
        internal List<bool> PassList { get; } = new();

        /// <summary>冻结：将内部列表转为数组，释放临时存储</summary>
        public void Freeze()
        {
            Geometries = GeometryList.ToArray();
            IsPassArray = PassList.ToArray();
        }

        private void OnStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GraphicsStyle.IsVisible))
                IsVisible = _style!.IsVisible;
            NotifyChanged();
        }
    }
}
