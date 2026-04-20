using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 工具属性元数据（在工具注册时提取，设计时和运行时共享）
    /// </summary>
    /// <remarks>
    /// 核心特性：
    /// - 编译时提取：在工具注册时通过反射一次性提取
    /// - 不依赖实例：通过临时空实例调用 GetPropertyMap
    /// - 变量池存储：存储在嵌套字典中，避免重复计算
    /// - 直接读取：使用时直接从变量池读取，性能最优
    /// 
    /// 注意：此类命名为 ToolPropertyMetadata 以避免与 System.Windows.PropertyMetadata 冲突。
    /// </remarks>
    public class ToolPropertyMetadata
    {
        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（中文，用于UI显示）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 分组名称（中文，用于UI树形分组显示）
        /// </summary>
        /// <remarks>
        /// 示例: "结果", "图像", "像素值", "文件", "执行信息"
        /// null 表示无分组，属性直接显示在节点下
        /// </remarks>
        public string? GroupName { get; set; }

        /// <summary>
        /// 属性类型
        /// </summary>
        public Type? PropertyType { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }
    }
}
