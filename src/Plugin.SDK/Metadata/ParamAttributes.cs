using System;

namespace SunEyeVision.Plugin.SDK.Metadata
{
    /// <summary>
    /// 忽略绑定特性 - 标记参数不支持数据绑定
    /// </summary>
    /// <remarks>
    /// 用于标记那些不支持从上游节点绑定的参数。
    ///
    /// 使用示例：
    /// <code>
    /// [IgnoreBind]  // 不支持从上游绑定，必须手动设置
    /// public ImageData? ReferenceImage { get; set; }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreBindAttribute : Attribute
    {
    }
}
