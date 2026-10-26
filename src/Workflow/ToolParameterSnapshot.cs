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

            return ToolExecutionPreparation.CopyParameters(source, WorkflowSerializationOptions.Compact);
        }
        /// <summary>按持久化常量的真实CLR类型深复制设置，避免历史共享可变数组/列表。</summary>
        public static ParamSettingContainer? CopySettings(ParamSettingContainer? source)
        {
            if(source==null)return null;
            var result=new ParamSettingContainer{NodeId=source.NodeId,ToolName=source.ToolName};
            foreach(var setting in source.Settings)
            {
                var copy=setting.Clone();var value=setting.ConstantValue;
                copy.ConstantValue=value==null?null:value is JsonElement element?element.Clone():
                    JsonSerializer.Deserialize(JsonSerializer.Serialize(value,value.GetType(),WorkflowSerializationOptions.Compact),value.GetType(),WorkflowSerializationOptions.Compact);
                result.SetSetting(copy);
            }
            return result;
        }
    }
}
