using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Workflow.Read begin | token={reader.TokenType}",
                "WorkflowJsonConverter");
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Workflow JSON parsed | properties={string.Join(',', root.EnumerateObject().Select(property => property.Name))}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");

            // 读取基本属性
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Basic properties read begin | elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");
            var id = root.GetProperty("Id").GetString() ?? Guid.NewGuid().ToString();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Id read completed | id={id}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");
            var name = root.GetProperty("Name").GetString() ?? "Unnamed Workflow";
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Name read completed | name={name}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");

            // 创建工作流实例
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Workflow constructor begin | elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");
            var workflow = new Workflow(id, name);
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Workflow instance created | id={id}, name={name}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");

            // 恢复子程序标记（防止加载后被子程序当成主工作流创建独立标签页）
            if (root.TryGetProperty("IsSubroutine", out var isSubroutineProp))
            {
                workflow.IsSubroutine = isSubroutineProp.GetBoolean();
            }

            // 直接反序列化节点（无需 Dictionary 转换层）
            // Phase 2：反序列化后通过工厂转换为 WorkflowNode 实例
            if (root.TryGetProperty("Nodes", out var nodesElement))
            {
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[WorkflowLoadTrace] Nodes begin | count={nodesElement.GetArrayLength()}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                    "WorkflowJsonConverter");
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

                        // 诊断日志：追踪输入源持久化恢复情况
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"节点反序列化 | Name={node.Name} | Type={node.GetType().Name} | " +
                            $"ImageSourceConfig={node.ImageSourceConfig?.FilePaths.Count ?? -1}",
                            "WorkflowJsonConverter");

                        workflow.Nodes.Add(node);
                    }
                }
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[WorkflowLoadTrace] Nodes completed | count={workflow.Nodes.Count}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                    "WorkflowJsonConverter");
            }

            // 反序列化连接（加载时一次性过滤孤立连接，运行时信任不变量）
            if (root.TryGetProperty("Connections", out var connElement))
            {
                try
                {
                    var connectionCount = connElement.ValueKind == JsonValueKind.Array
                        ? connElement.GetArrayLength()
                        : -1;
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[WorkflowLoadTrace] Connections begin | count={connectionCount}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                        "WorkflowJsonConverter");

                    // 空连接集合无需经过带有工作流序列化选项的通用反序列化路径。
                    // 该路径可能触发不必要的类型/转换器初始化；空数组直接创建集合，
                    // 同时保留与非空连接一致的生命周期日志。
                    if (connectionCount == 0)
                    {
                        workflow.Connections = new ObservableCollection<WorkflowConnection>();
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"[WorkflowLoadTrace] Connections completed | count=0, path=empty-fast-path, elapsedMs={stopwatch.ElapsedMilliseconds}",
                            "WorkflowJsonConverter");
                        VisionLogger.Instance.Log(LogLevel.Info,
                            $"[WorkflowLoadTrace] Workflow.Read completed | id={workflow.Id}, path=empty-connections-fast-path, elapsedMs={stopwatch.ElapsedMilliseconds}",
                            "WorkflowJsonConverter");
                        return workflow;
                    }

                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[WorkflowLoadTrace] Connections deserialize begin | count={connectionCount}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                        "WorkflowJsonConverter");
                    var connList = JsonSerializer.Deserialize<List<WorkflowConnection>>(
                        connElement.GetRawText(),
                        options
                    );
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[WorkflowLoadTrace] Connections deserialize returned | count={connList?.Count ?? 0}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                        "WorkflowJsonConverter");
                    // 收集所有合法节点 ID
                    var validNodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.Id));
                    var filteredCount = 0;
                    var validConnections = (connList ?? new())
                        .Where(c =>
                        {
                            if (validNodeIds.Contains(c.SourceNodeId)
                                && validNodeIds.Contains(c.TargetNodeId))
                                return true;
                            filteredCount++;
                            return false;
                        })
                        .ToList();
                    if (filteredCount > 0)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"已过滤 {filteredCount} 个孤立连接（指向不存在节点）",
                            "WorkflowJsonConverter");
                    }
                    workflow.Connections = new ObservableCollection<WorkflowConnection>(validConnections);
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[WorkflowLoadTrace] Connections completed | count={workflow.Connections.Count}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                        "WorkflowJsonConverter");
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

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[WorkflowLoadTrace] Workflow.Read completed | id={workflow.Id}, elapsedMs={stopwatch.ElapsedMilliseconds}",
                "WorkflowJsonConverter");
            return workflow;
        }

        public override void Write(Utf8JsonWriter writer, Workflow value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // 写入基本属性
            writer.WriteString("Id", value.Id);
            writer.WriteString("Name", value.Name);
            writer.WriteBoolean("IsSubroutine", value.IsSubroutine);

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
