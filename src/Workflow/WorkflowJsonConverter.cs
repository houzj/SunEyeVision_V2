using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 工作流 JSON 转换器 - 直接序列化 WorkflowNodeBase
    /// </summary>
    /// <remarks>
    /// 优化说明（rule-010: 方案系统实现规范）：
    /// - 删除 Dictionary 转换层，直接序列化 WorkflowNodeBase 对象
    /// - System.Text.Json 自动处理多态序列化（如有子类需要 [JsonPolymorphic]）
    /// - Parameters 通过 ToolParameters 自身的 [JsonPolymorphic] 处理多态
    /// </remarks>
    public class WorkflowJsonConverter : JsonConverter<Workflow>
    {
        public override Workflow Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;

            // 读取基本属性
            var id = root.GetProperty("Id").GetString() ?? Guid.NewGuid().ToString();
            var name = root.GetProperty("Name").GetString() ?? "Unnamed Workflow";

            // 创建工作流实例
            var workflow = new Workflow(id, name);

            // 直接反序列化节点（无需 Dictionary 转换层）
            // Phase 2：反序列化后通过工厂转换为 WorkflowNode 实例
            if (root.TryGetProperty("Nodes", out var nodesElement))
            {
                workflow.Nodes = new ObservableCollection<WorkflowNodeBase>();
                foreach (var nodeElement in nodesElement.EnumerateArray())
                {
                    var node = JsonSerializer.Deserialize<WorkflowNodeBase>(
                        nodeElement.GetRawText(), options);
                    if (node != null)
                    {
                        // Phase 2：通过工厂转换为强类型实例（WorkflowNode）
                        if (WorkflowNodeFactory.TypeConverter != null)
                        {
                            node = WorkflowNodeFactory.TypeConverter(node);
                        }

                        // 调用反序列化后回调
                        node.OnDeserialized();

                        workflow.Nodes.Add(node);
                    }
                }
            }

            // 反序列化连接
            if (root.TryGetProperty("Connections", out var connElement))
            {
                try
                {
                    var connList = JsonSerializer.Deserialize<List<WorkflowConnection>>(
                        connElement.GetRawText(),
                        options
                    );
                    workflow.Connections = connList != null
                        ? new ObservableCollection<WorkflowConnection>(connList)
                        : new ObservableCollection<WorkflowConnection>();
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning, $"Failed to deserialize connections: {ex.Message}", "WorkflowJsonConverter");
                    workflow.Connections = new ObservableCollection<WorkflowConnection>();
                }
            }
            else
            {
                workflow.Connections = new ObservableCollection<WorkflowConnection>();
            }

            return workflow;
        }

        public override void Write(Utf8JsonWriter writer, Workflow value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // 写入基本属性
            writer.WriteString("Id", value.Id);
            writer.WriteString("Name", value.Name);

            // 直接序列化节点（无需 Dictionary 转换层）
            writer.WritePropertyName("Nodes");
            writer.WriteStartArray();
            foreach (var node in value.Nodes)
            {
                JsonSerializer.Serialize(writer, node, options);
            }
            writer.WriteEndArray();

            // 写入连接
            writer.WritePropertyName("Connections");
            JsonSerializer.Serialize(writer, value.Connections, options);

            writer.WriteEndObject();
        }
    }
}
