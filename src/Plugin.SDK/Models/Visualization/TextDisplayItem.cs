using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 文本显示项 - 用于渲染文本标签、统计信息等文本内容
    /// 样式由 Style 引用提供（与 Parameters/UI 共享同一实例）
    /// </summary>
    public sealed class TextDisplayItem : DisplayItemBase
    {
        /// <summary>文本样式引用</summary>
        [JsonIgnore]
        public TextStyle? Style { get; set; }

        /// <summary>整体通过/失败状态（控制 OkColor/NgColor 切换）</summary>
        public bool IsPass { get; set; } = true;

        /// <summary>文本数据（运行时，不序列化）</summary>
        [JsonIgnore]
        public DisplayTextData[] Texts { get; set; } = Array.Empty<DisplayTextData>();

        /// <summary>锚点X坐标（当所有Texts坐标相同时作为起点自动换行，运行时）</summary>
        [JsonIgnore]
        public double AnchorX { get; set; }

        /// <summary>锚点Y坐标（当所有Texts坐标相同时作为起点自动换行，运行时）</summary>
        [JsonIgnore]
        public double AnchorY { get; set; }
    }
}
