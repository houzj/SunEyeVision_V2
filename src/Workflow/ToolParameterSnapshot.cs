using System;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 为一次工作流运行创建与设计器参数隔离的配置快照。
    /// </summary>
    public static class ToolParameterSnapshot
    {
        /// <summary>
        /// 使用项目持久化所采用的 JSON 契约复制参数，因此集合、嵌套配置和绑定描述也会被复制。
        /// JsonIgnore 标记的运行数据不会进入快照。
        /// </summary>
        public static ToolParameters Create(ToolParameters source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var parameterType = source.GetType();
            var json = JsonSerializer.Serialize(source, parameterType, WorkflowSerializationOptions.Compact);
            return (ToolParameters)(JsonSerializer.Deserialize(
                json,
                parameterType,
                WorkflowSerializationOptions.Compact)
                ?? throw new InvalidOperationException($"无法为参数类型 {parameterType.FullName} 创建运行快照。"));
        }
    }
}
