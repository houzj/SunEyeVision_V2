using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 工作流节点工厂 - 从ToolRegistry创建工作流节点
    /// </summary>
    public static class WorkflowNodeFactory
    {
        /// <summary>
        /// 节点类型转换器。UI 层在启动时注册，确保反序列化后
        /// 所有 WorkflowNodeBase 实例自动转换为 WorkflowNode 实例。
        /// Phase 2 优化：消除节点副本，实现对象级共享。
        /// </summary>
        public static Func<WorkflowNodeBase, WorkflowNodeBase>? TypeConverter { get; set; }
        /// <summary>
        /// 从工具元数据创建算法节点
        /// </summary>
        /// <param name="toolId">工具ID(统一标识符)</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="parameters">算法参数</param>
        /// <param name="enableCaching">是否启用缓存</param>
        /// <param name="enableRetry">是否启用重试</param>
        /// <returns>创建的AlgorithmNode,如果工具不存在则返回null</returns>
        public static AlgorithmNode? CreateNode(
            string toolId,
            string nodeId,
            string nodeName,
            string dispName,
            ToolParameters? parameters = null,
            bool enableCaching = true,
            bool enableRetry = false)
        {
            return CreateAlgorithmNode(toolId, nodeId, nodeName, dispName, parameters, enableCaching, enableRetry);
        }

        /// <summary>
        /// 从工具ID创建AlgorithmNode
        /// </summary>
        /// <param name="toolId">工具ID(统一标识符)</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="parameters">算法参数</param>
        /// <param name="enableCaching">是否启用缓存</param>
        /// <param name="enableRetry">是否启用重试</param>
        /// <returns>创建的AlgorithmNode,如果工具不存在则返回null</returns>
        public static AlgorithmNode? CreateAlgorithmNode(
            string toolId,
            string nodeId,
            string nodeName,
            string dispName,
            ToolParameters? parameters = null,
            bool enableCaching = true,
            bool enableRetry = false)
        {
            try
            {
                // 从ToolRegistry获取工具元数据
                var metadata = ToolRegistry.GetToolMetadata(toolId);
                if (metadata == null)
                {
return null;
                }

                // 创建工具实例
                var tool = ToolRegistry.CreateToolInstance(toolId);
                if (tool == null)
                {
return null;
                }

                // 设置参数
                if (parameters == null)
                {
                    parameters = ToolRegistry.CreateParameters(toolId);
                }

                // 创建AlgorithmNode
                var algorithmNode = new AlgorithmNode(nodeId, nodeName, dispName, tool)
                {
                    Parameters = parameters,
                    DebugWindowType = metadata.DebugWindowType,
                    NodeStyleType = metadata.NodeStyleType
                };
return algorithmNode;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 从工具ID批量创建AlgorithmNode
        /// </summary>
        /// <param name="toolConfig">工具配置列表</param>
        /// <returns>创建的AlgorithmNode列表</returns>
        public static List<AlgorithmNode> CreateAlgorithmNodes(IEnumerable<ToolNodeConfig> toolConfig)
        {
            var nodes = new List<AlgorithmNode>();

            foreach (var config in toolConfig)
            {
                var node = CreateAlgorithmNode(
                    config.ToolId,
                    config.NodeId,
                    config.NodeName,
                    config.DispName,
                    config.Parameters,
                    config.EnableCaching,
                    config.EnableRetry);

                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        // Start 节点已废弃，不再需要特殊节点类型
        // 执行顺序由连线关系决定，基于入度自动识别入口节点

        /// <summary>
        /// 创建子程序节点
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="parameters">算法参数</param>
        /// <returns>创建的SubroutineNode</returns>
        public static SubroutineNode CreateSubroutineNode(
            string nodeId,
            string nodeName,
            string dispName,
            ToolParameters? parameters = null)
        {
            var node = new SubroutineNode(nodeId, nodeName, dispName);
            if (parameters != null)
            {
                node.Parameters = parameters;
            }
            return node;
        }

        /// <summary>
        /// 创建条件节点
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="conditionExpression">条件表达式</param>
        /// <param name="parameters">算法参数</param>
        /// <returns>创建的ConditionNode</returns>
        public static ConditionNode CreateConditionNode(
            string nodeId,
            string nodeName,
            string dispName,
            string conditionExpression,
            ToolParameters? parameters = null)
        {
            var node = new ConditionNode(nodeId, nodeName, dispName);
            node.SetExpressionCondition(conditionExpression);

            if (parameters != null)
            {
                node.Parameters = parameters;
            }
            return node;
        }

        /// <summary>
        /// 验证工具是否可用
        /// </summary>
        public static bool ValidateTool(string toolId)
        {
            var metadata = ToolRegistry.GetToolMetadata(toolId);
            // IsEnabled 字段已从 ToolMetadata 移除，工具始终可用
            return metadata != null;
        }

        /// <summary>
        /// 获取工具元数据
        /// </summary>
        public static ToolMetadata? GetToolMetadata(string toolId)
        {
            return ToolRegistry.GetToolMetadata(toolId);
        }
    }

    /// <summary>
    /// 工具节点配置
    /// </summary>
    public class ToolNodeConfig
    {
        /// <summary>
        /// 工具ID(统一标识符)
        /// </summary>
        public string ToolId { get; set; } = string.Empty;

        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点名称（包含全局序号，用于序列化）
        /// </summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// 节点显示名称（不含序号，用于UI显示）
        /// </summary>
        public string DispName { get; set; } = string.Empty;

        /// <summary>
        /// 算法参数
        /// </summary>
        public ToolParameters? Parameters { get; set; }

        /// <summary>
        /// 是否启用缓存
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// 是否启用重试
        /// </summary>
        public bool EnableRetry { get; set; } = false;
    }
}
