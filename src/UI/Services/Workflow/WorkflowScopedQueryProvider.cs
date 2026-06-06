using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using WorkflowModel = SunEyeVision.Workflow.Workflow;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 工作流作用域查询提供者 — 将 IWorkflowConnectionProvider 和 INodeInfoProvider
    /// 的作用域限定到特定 Workflow 实例，而非使用 WorkflowEngine.CurrentWorkflow。
    /// 用于子程序参数对话框的数据源查询，使其能访问子程序工作流内部的节点。
    /// </summary>
    public class WorkflowScopedQueryProvider : IWorkflowConnectionProvider, INodeInfoProvider
    {
        private readonly WorkflowModel _workflow;

        public WorkflowScopedQueryProvider(WorkflowModel workflow)
        {
            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        }

        #region IWorkflowConnectionProvider

        public List<string> GetParentNodeIds(string nodeId)
        {
            if (_workflow.Connections == null)
                return new List<string>();

            return _workflow.Connections
                .Where(conn => conn.TargetNodeId == nodeId)
                .Select(conn => conn.SourceNodeId)
                .Distinct()
                .ToList();
        }

        public List<string> GetChildNodeIds(string nodeId)
        {
            if (_workflow.Connections == null)
                return new List<string>();

            return _workflow.Connections
                .Where(conn => conn.SourceNodeId == nodeId)
                .Select(conn => conn.TargetNodeId)
                .Distinct()
                .ToList();
        }

        public List<string> GetAllNodeIds()
        {
            return _workflow.Nodes?.Select(n => n.Id).ToList() ?? new List<string>();
        }

        #endregion

        #region INodeInfoProvider

        public string GetNodeName(string nodeId)
        {
            var node = _workflow.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            return node?.Name ?? nodeId;
        }

        public string GetNodeType(string nodeId)
        {
            var node = _workflow.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            return node?.ToolType ?? "Unknown";
        }

        public string? GetNodeIcon(string nodeId)
        {
            var node = _workflow.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            return node?.Icon;
        }

        public bool NodeExists(string nodeId)
        {
            return _workflow.Nodes?.Any(n => n.Id == nodeId) ?? false;
        }

        public Type? GetResultType(string nodeId)
        {
            var node = _workflow.Nodes?.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
                return null;

            var toolMetadata = SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.GetToolMetadata(node.ToolType);
            var toolType = toolMetadata?.ToolType;
            if (toolType == null)
                return null;

            foreach (var iface in toolType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(SunEyeVision.Plugin.SDK.Core.IToolPlugin<,>))
                {
                    var genericArgs = iface.GetGenericArguments();
                    if (genericArgs.Length >= 2)
                        return genericArgs[1];
                }
            }

            return null;
        }

        #endregion
    }
}