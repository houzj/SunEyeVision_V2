using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Plugin.SDK.Metadata
{
    /// <summary>
    /// 工具元数据 - 描述工具的核心信息
    /// </summary>
    /// <remarks>
    /// 优化说明：
    /// - 移除运行时特性：支持并行、副作用、缓存等运行时特性不应存储在静态元数据中
    /// - 移除冗余字段：Version, Author, IsEnabled, HasDebugInterface 应从 [Tool] 特性读取
    /// - 类型信息作为计算属性：从 AlgorithmType 动态推断 ParamsType 和 ResultType
    /// 
    /// Single Source of Truth 原则：
    /// 工具定义的唯一来源是 [Tool] 特性和 AlgorithmType。
    /// </remarks>
    public class ToolMetadata
    {
        #region 核心字段（6个）

        /// <summary>
        /// 工具ID (唯一标识符)
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// 显示名称 (UI显示)
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 工具描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工具图标
        /// </summary>
        public string Icon { get; set; } = "generic";

        /// <summary>Read ordering from the same tool attribute as the rest of metadata.</summary>
        [JsonIgnore]
        public int Order => ToolType == null ? 1000 : ((ToolAttribute?)Attribute.GetCustomAttribute(ToolType, typeof(ToolAttribute)))?.Order ?? 1000;

        /// <summary>
        /// 工具分类
        /// </summary>
        public string Category { get; set; } = "未分类";

        #endregion

        #region 类型信息

        /// <summary>
        /// 算法类型 - 工具实现类
        /// </summary>
        /// <remarks>
        /// 算法类型必须实现 IToolPlugin 接口。
        /// 类型信息从 AlgorithmType 动态推断。
        /// </remarks>
        public Type? ToolType { get; set; }

        /// <summary>
        /// 调试窗口类型 - 工具专用的调试窗口
        /// </summary>
        /// <remarks>
        /// 工具可以指定自己的调试窗口类型，以便在节点双击时打开。
        /// 如果为 null，则使用默认的调试窗口创建机制。
        /// </remarks>
        [JsonIgnore]
        public Type? DebugWindowType { get; set; }

        /// <summary>
        /// 调试窗口样式 - 定义窗口打开行为
        /// </summary>
        /// <remarks>
        /// 窗口类型优先级：节点级配置 → 工具级配置 → 全局默认值
        /// - None: 无窗口（不打开任何窗口）
        /// - Default: 标准窗口（有标题栏和边框）
        /// - Custom: 自定义窗口（无边框圆角窗口）
        /// </remarks>
        [JsonIgnore]
        public DebugWindowStyle DebugWindowStyle { get; set; } = DebugWindowStyle.Default;

        /// <summary>
        /// 节点样式类型 - 工具专用的节点样式
        /// </summary>
        /// <remarks>
        /// 工具可以指定自己的节点样式类型，以便节点根据工具类型使用不同的样式。
        /// 如果为 null，则使用默认的 StandardNodeStyle。
        /// </remarks>
        [JsonIgnore]
        public Type? NodeStyleType { get; set; }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 从工具类型创建元数据
        /// </summary>
        public static ToolMetadata FromToolType(Type toolType, string id, string displayName, string description, string icon, string category)
        {
            return new ToolMetadata
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Icon = icon,
                Category = category,
                ToolType = toolType
            };
        }

        #endregion

        #region 验证与克隆

        /// <summary>
        /// 验证元数据完整性
        /// </summary>
        public ValidationResult Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(Id))
                errors.Add("工具ID不能为空");

            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(string.Join("; ", errors));
        }

        /// <summary>
        /// 创建浅拷贝
        /// </summary>
        public ToolMetadata Clone()
        {
            return new ToolMetadata
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                Icon = Icon,
                Category = Category,
                ToolType = ToolType,
                DebugWindowType = DebugWindowType,
                NodeStyleType = NodeStyleType
            };
        }

        /// <summary>
        /// 生成工具标识字符串
        /// </summary>
        public override string ToString() => $"{DisplayName} [{Category}]";

        #endregion
    }
}
