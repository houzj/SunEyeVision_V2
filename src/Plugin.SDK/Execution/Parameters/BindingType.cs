using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数绑定类型
    /// </summary>
    /// <remarks>
    /// 定义参数值的来源方式，支持以下绑定类型：
    /// - Constant: 常量绑定，使用固定值
    /// - Binding: 动态绑定，从其他节点输出获取值
    /// - NodeOutput: 节点输出绑定，从其他节点的输出端口获取值
    /// 
    /// 使用示例：
    /// <code>
    /// // 常量绑定
    /// var constantBinding = new ConstantSource(128);
    /// 
    /// // 节点输出绑定
    /// var nodeOutputBinding = new NodeOutputSource("node_001", "OutputImage");
    /// </code>
    /// </remarks>
    public enum BindingType
    {
        /// <summary>
        /// 常量绑定 - 使用固定值
        /// </summary>
        /// <remarks>
        /// 参数使用固定的常量值，不依赖其他节点的输出。
        /// 适用于需要手动设置参数值的场景。
        /// 数据类型由 System.Type 定义，支持精确类型匹配。
        /// </remarks>
        Constant = 0,

        /// <summary>
        /// 动态绑定 - 从其他节点输出获取值
        /// </summary>
        /// <remarks>
        /// 参数值从其他节点的输出属性动态获取。
        /// 支持运行时自动解析，实现节点间的数据传递。
        /// </remarks>
        Binding = 1,

        /// <summary>
        /// 节点输出绑定 - 从其他节点的输出端口获取值
        /// </summary>
        /// <remarks>
        /// 参数值从其他节点的指定输出端口动态获取。
        /// 支持多值输出通过索引选择。
        /// 可选地使用属性路径访问复合对象的属性。
        /// </remarks>
        NodeOutput = 2
    }
}
