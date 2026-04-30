using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 文本显示项 - 用于渲染文本标签、统计信息等文本内容
    /// 样式由 Style 引用提供（与 Parameters/UI 共享同一实例）
    /// Style 变更时自动同步 IsVisible 并触发重绘
    /// </summary>
    public sealed class TextDisplayItem : DisplayItemBase
    {
        private TextStyle? _style;

        /// <summary>文本样式引用</summary>
        [JsonIgnore]
        public TextStyle? Style
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

        /// <summary>逐元素通过标记（与 Texts 一一对应，空数组表示全通过）</summary>
        [JsonIgnore]
        public bool[] IsPassArray { get; set; } = Array.Empty<bool>();

        /// <summary>文本数据（运行时，不序列化）</summary>
        [JsonIgnore]
        public DisplayTextData[] Texts { get; set; } = Array.Empty<DisplayTextData>();

        /// <summary>锚点X坐标（当所有Texts坐标相同时作为起点自动换行，运行时）</summary>
        [JsonIgnore]
        public double AnchorX { get; set; }

        /// <summary>锚点Y坐标（当所有Texts坐标相同时作为起点自动换行，运行时）</summary>
        [JsonIgnore]
        public double AnchorY { get; set; }

        private void OnStylePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TextStyle.IsVisible))
                IsVisible = _style!.IsVisible;
            NotifyChanged();
        }
    }
}
