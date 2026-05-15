using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using HalconDotNet;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Workflow Engine
    /// </summary>
    public class WorkflowEngine : IWorkflowConnectionProvider, INodeInfoProvider
    {
        /// <summary>
        /// Workflow list
        /// </summary>
        private Dictionary<string, Workflow> Workflows { get; set; }

        /// <summary>
        /// Current active workflow
        /// </summary>
        public Workflow CurrentWorkflow { get; private set; }

        /// <summary>
        /// Logger
        /// </summary>
        private ILogger Logger { get; set; }

        public WorkflowEngine(ILogger logger)
        {
            Logger = logger;
            Workflows = new Dictionary<string, Workflow>();
        }

        /// <summary>
        /// Create new workflow
        /// </summary>
        public Workflow CreateWorkflow(string id, string name, string description = "")
        {
            if (Workflows.ContainsKey(id))
            {
                throw new ArgumentException($"Workflow ID {id} already exists");
            }

            var workflow = new Workflow
            {
                Id = id,
                Name = name
            };

            Workflows[id] = workflow;
            Logger.LogInfo($"Created workflow {name} (ID: {id})");

            return workflow;
        }

        /// <summary>
        /// Delete workflow
        /// </summary>
        public bool DeleteWorkflow(string id)
        {
            if (Workflows.TryGetValue(id, out var workflow))
            {
                Workflows.Remove(id);
                
                if (CurrentWorkflow?.Id == id)
                {
                    CurrentWorkflow = null;
                }

                Logger.LogInfo($"Deleted workflow {workflow.Name} (ID: {id})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Register workflow (用于外部加载的工作流同步注册)
        /// </summary>
        public void RegisterWorkflow(Workflow workflow)
        {
            if (workflow == null)
            {
                throw new ArgumentNullException(nameof(workflow));
            }

            if (string.IsNullOrEmpty(workflow.Id))
            {
                throw new ArgumentException("Workflow ID cannot be null or empty");
            }

            if (Workflows.ContainsKey(workflow.Id))
            {
                // 工作流已存在，更新它
                Workflows[workflow.Id] = workflow;
                Logger.LogInfo($"Updated workflow {workflow.Name} (ID: {workflow.Id})");
            }
            else
            {
                // 添加新工作流
                Workflows[workflow.Id] = workflow;
                Logger.LogInfo($"Registered workflow {workflow.Name} (ID: {workflow.Id})");
            }
        }

        /// <summary>
        /// Clear all workflows (用于重置引擎状态)
        /// </summary>
        public void ClearWorkflows()
        {
            Workflows.Clear();
            CurrentWorkflow = null;
            Logger.LogInfo("All workflows cleared from engine");
        }

        /// <summary>
        /// Set current workflow
        /// </summary>
        public void SetCurrentWorkflow(string id)
        {
            if (!Workflows.ContainsKey(id))
            {
                throw new ArgumentException($"Workflow ID {id} does not exist");
            }

            CurrentWorkflow = Workflows[id];
            Logger.LogInfo($"Set current workflow: {CurrentWorkflow.Name}");
        }

        /// <summary>
        /// Get workflow
        /// </summary>
        public Workflow GetWorkflow(string id)
        {
            Workflows.TryGetValue(id, out var workflow);
            return workflow;
        }

        /// <summary>
        /// Get workflow by ID (alias for GetWorkflow)
        /// </summary>
        public Workflow? GetWorkflowById(string workflowId)
        {
            Workflows.TryGetValue(workflowId, out var workflow);
            return workflow;
        }

        /// <summary>
        /// Get all workflows
        /// </summary>
        public List<Workflow> GetAllWorkflows()
        {
            return Workflows.Values.ToList();
        }

        /// <summary>
        /// Execute workflow
        /// </summary>
        public List<AlgorithmResult> ExecuteWorkflow(string workflowId, HImage inputImage)
        {
            if (!Workflows.ContainsKey(workflowId))
            {
                Logger.LogError($"Workflow does not exist: {workflowId}");
                return new List<AlgorithmResult>();
            }

            var workflow = Workflows[workflowId];
            Logger.LogInfo($"Executing workflow: {workflow.Name}");

            // Workflow.Execute 已迁移到 WorkflowExecutionEngine
            // var results = workflow.Execute(inputImage);
            var results = new List<AlgorithmResult>();

            return results;
        }

        /// <summary>
        /// Execute current workflow
        /// </summary>
        public List<AlgorithmResult> ExecuteCurrentWorkflow(HImage inputImage)
        {
            if (CurrentWorkflow == null)
            {
                Logger.LogWarning("No active workflow");
                return new List<AlgorithmResult>();
            }

            return ExecuteWorkflow(CurrentWorkflow.Id, inputImage);
        }

        /// <summary>
        /// Save workflow to file
        /// </summary>
        public bool SaveWorkflow(string workflowId, string filePath)
        {
            if (!Workflows.ContainsKey(workflowId))
            {
                Logger.LogError($"Workflow does not exist: {workflowId}");
                return false;
            }

            try
            {
                var workflow = Workflows[workflowId];

                // 使用 Workflow 层的序列化配置
                var options = WorkflowSerializationOptions.Default;

                var json = JsonSerializer.Serialize(workflow, options);
                File.WriteAllText(filePath, json);

                Logger.LogInfo($"Workflow saved to: {filePath}");
                Logger.LogInfo($"  - Saved {workflow.Nodes.Count} nodes and {workflow.Connections.Count} connections");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save workflow: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Load workflow from file
        /// </summary>
        public bool LoadWorkflow(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.LogError($"File does not exist: {filePath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(filePath);

                // 使用 Workflow 层的序列化配置
                var options = WorkflowSerializationOptions.Default;

                var workflow = JsonSerializer.Deserialize<Workflow>(json, options);

                if (workflow != null)
                {
                    // Logger is not assigned to Workflow anymore
                    // workflow.SetLogger(Logger);

                    // Ensure collections are initialized if null
                    if (workflow.Nodes == null)
                    {
                        workflow.Nodes = new ObservableCollection<WorkflowNodeBase>();
                    }

                    if (workflow.Connections == null)
                    {
                        workflow.Connections = new ObservableCollection<WorkflowConnection>();
                    }

                    Workflows[workflow.Id] = workflow;
                    CurrentWorkflow = workflow;

                    Logger.LogInfo($"Workflow loaded from file: {workflow.Name}");
                    Logger.LogInfo($"  - Loaded {workflow.Nodes.Count} nodes and {workflow.Connections.Count} connections");
                    return true;
                }

                Logger.LogError("Failed to load workflow: Deserialization returned null");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load workflow: {ex.Message}", ex);
                return false;
            }
        }

        #region IWorkflowConnectionProvider 实现

        /// <inheritdoc/>
        /// <summary>
        /// 递归查找所有上游节点（包括直接父节点和间接父节点）
        /// 使用 BFS 遍历，保证节点按照距离顺序返回
        /// 使用 List 保持顺序，使用 HashSet 去重，避免循环依赖
        /// </summary>
        public List<string> GetParentNodeIds(string nodeId)
        {
            if (CurrentWorkflow == null)
            {
                return new List<string>();
            }

            // BFS 递归查找所有上游节点
            var upstreamNodeIds = new List<string>();  // 使用 List 保持 BFS 顺序
            var queue = new Queue<string>();
            var visited = new HashSet<string>();

            queue.Enqueue(nodeId);
            visited.Add(nodeId);

            int bfsStep = 0;
            while (queue.Count > 0)
            {
                var currentNodeId = queue.Dequeue();
                bfsStep++;
                Logger.LogInfo($"  🔄 BFS 步骤 {bfsStep}: 处理节点 {currentNodeId}", "WorkflowEngine");

                // 查找当前节点的所有父节点（连接指向当前节点的）
                // 🔍 按连接创建顺序排序（使用索引）
                var parentConnections = CurrentWorkflow.Connections
                    .Select((conn, index) => new { Connection = conn, Index = index })
                    .Where(x => x.Connection.TargetNodeId == currentNodeId)
                    .OrderBy(x => x.Index)  // ✅ 按连接创建顺序排序
                    .ToList();
                    
                Logger.LogInfo($"    找到 {parentConnections.Count} 个连接指向此节点", "WorkflowEngine");

                foreach (var item in parentConnections)
                {
                    var connection = item.Connection;
                    Logger.LogInfo($"      [{item.Index}] 连接: {connection.SourceNodeId} → {connection.TargetNodeId}", "WorkflowEngine");
                    
                    if (!visited.Contains(connection.SourceNodeId))
                    {
                        visited.Add(connection.SourceNodeId);
                        upstreamNodeIds.Add(connection.SourceNodeId);  // 添加到有序列表
                        queue.Enqueue(connection.SourceNodeId);
                        Logger.LogInfo($"        ✅ 添加到结果: {connection.SourceNodeId}", "WorkflowEngine");
                    }
                    else
                    {
                        Logger.LogInfo($"        ⏭️ 已访问，跳过: {connection.SourceNodeId}", "WorkflowEngine");
                    }
                }
            }

            Logger.LogInfo($"找到 {upstreamNodeIds.Count} 个父节点: [{string.Join(", ", upstreamNodeIds)}]", "WorkflowEngine");
            Logger.LogInfo($"============================================", "WorkflowEngine");
            return upstreamNodeIds;
        }

        /// <inheritdoc/>
        public List<string> GetChildNodeIds(string nodeId)
        {
            if (CurrentWorkflow == null)
                return new List<string>();

            return CurrentWorkflow.Connections
                .Where(conn => conn.SourceNodeId == nodeId)
                .Select(conn => conn.TargetNodeId)
                .ToList();
        }

        /// <inheritdoc/>
        public List<string> GetAllNodeIds()
        {
            return CurrentWorkflow?.Nodes?.Select(n => n.Id).ToList() ?? new List<string>();
        }

        #endregion

        #region INodeInfoProvider 实现

        /// <inheritdoc/>
        public string GetNodeName(string nodeId)
        {
            var node = CurrentWorkflow?.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            var nodeName = node?.Name ?? nodeId;
            Logger.LogInfo($"  GetNodeName({nodeId}) = {nodeName}", "WorkflowEngine");
            return nodeName;
        }

        /// <inheritdoc/>
        public string GetNodeType(string nodeId)
        {
            var node = CurrentWorkflow?.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            var nodeType = node?.ToolType ?? "Unknown";
            Logger.LogInfo($"  GetNodeType({nodeId}) = {nodeType}", "WorkflowEngine");
            return nodeType;
        }

        /// <inheritdoc/>
        public string? GetNodeIcon(string nodeId)
        {
            var node = CurrentWorkflow?.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            return node?.Icon;
        }

        /// <inheritdoc/>
        public bool NodeExists(string nodeId)
        {
            return CurrentWorkflow?.Nodes?.Any(n => n.Id == nodeId) ?? false;
        }

        /// <inheritdoc/>
        public Type? GetResultType(string nodeId)
        {
            var node = CurrentWorkflow?.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return null;
            }

            // 从工具元数据中获取 ToolType
            var toolMetadata = ToolRegistry.GetToolMetadata(node.ToolType);
            Type? toolType = toolMetadata?.ToolType;

            if (toolType == null)
            {
                Logger.LogWarning($"  GetResultType({nodeId}) - ToolType not found for {node.ToolType}", "WorkflowEngine");
                return null;
            }

            // 从 ToolType 推断 ResultType
            Type? resultType = null;

            // 检查 ToolType 是否实现了 IToolPlugin<TParams, TResult>
            foreach (var iface in toolType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(SunEyeVision.Plugin.SDK.Core.IToolPlugin<,>))
                {
                    var genericArgs = iface.GetGenericArguments();
                    if (genericArgs.Length >= 2)
                    {
                        resultType = genericArgs[1]; // TResult 是第二个泛型参数
                        break;
                    }
                }
            }

            Logger.LogInfo($"  GetResultType({nodeId}) = {resultType?.Name ?? "null"} (from {toolType.Name})", "WorkflowEngine");
            return resultType;
        }

        #endregion
    }
}
