using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 图形显示项 - 用于渲染点/线/圆/矩形等几何图形
    /// 样式由 Style 引用提供（与 Parameters/UI 共享同一实例）
    /// </summary>
    public sealed class GraphicsDisplayItem : DisplayItemBase
    {
        private bool _isFilled;

        /// <summary>图形样式引用（与UI控件、Parameters共享同一实例）</summary>
        [JsonIgnore]
        public GraphicsStyle? Style { get; set; }

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
    }
}
