using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>
/// 执行上下文（运行时临时对象）
/// </summary>
/// <remarks>
/// 提供运行时执行环境，包含工作流和全局变量。
/// 与 RuntimeConfig（持久化配置）不同，RunContext 是临时的运行时对象。
///
/// 使用场景：
/// 1. 执行检测任务时创建
/// 2. 提供节点参数查询（从节点内部获取）
/// 3. 任务完成后释放
///
/// 生命周期：
/// - 创建：开始执行时
/// - 使用：执行过程中
/// - 释放：执行完成后
/// </remarks>
public class RunContext
{
    private readonly List<GlobalVariable> _globalVariables;

    /// <summary>
    /// 工作流（执行流）
    /// </summary>
    public Workflow Workflow { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; } = DateTime.Now;

    /// <summary>
    /// 执行ID
    /// </summary>
    public string ExecutionId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 构造函数
    /// </summary>
    public RunContext(List<GlobalVariable> globalVariables)
    {
        _globalVariables = globalVariables ?? new List<GlobalVariable>();
    }

    /// <summary>
    /// 获取节点参数
    /// </summary>
    /// <remarks>
    /// 参数已嵌入在节点中，直接从节点获取
    /// </remarks>
    public ToolParameters? GetNodeParameters(string nodeId)
    {
        var node = Workflow.GetNode(nodeId);
        return node?.Parameters;
    }

    /// <summary>
    /// 获取节点参数（指定类型）
    /// </summary>
    public T? GetNodeParameters<T>(string nodeId) where T : ToolParameters
    {
        var parameters = GetNodeParameters(nodeId);
        return parameters as T;
    }

    /// <summary>
    /// 获取全局变量值
    /// </summary>
    public T? GetGlobalVariable<T>(string name)
    {
        var variable = _globalVariables.FirstOrDefault(v => v.Name == name);
        if (variable == null)
            return default;

        if (variable.Value is T typedValue)
            return typedValue;

        return default;
    }

    /// <summary>
    /// 设置全局变量值
    /// </summary>
    public void SetGlobalVariable(string name, object? value)
    {
        var variable = _globalVariables.FirstOrDefault(v => v.Name == name);
        if (variable != null)
        {
            variable.Value = value;
        }
    }

    /// <summary>
    /// 获取所有节点ID
    /// </summary>
    public List<string> GetAllNodeIds()
    {
        var nodeIds = new List<string>();
        foreach (var node in Workflow.Nodes)
        {
            nodeIds.Add(node.Id);
        }
        return nodeIds;
    }

    /// <summary>
    /// 获取启用的节点列表
    /// </summary>
    public List<WorkflowNodeBase> GetEnabledNodes()
    {
        var nodes = new List<WorkflowNodeBase>();
        foreach (var node in Workflow.Nodes)
        {
            if (node.IsEnabled)
                nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>
    /// 获取节点的输入连接
    /// </summary>
    public List<WorkflowConnection> GetInputConnections(string nodeId)
    {
        var connections = new List<WorkflowConnection>();
        foreach (var conn in Workflow.Connections)
        {
            if (conn.TargetNodeId == nodeId)
                connections.Add(conn);
        }
        return connections;
    }

    /// <summary>
    /// 获取节点的输出连接
    /// </summary>
    public List<WorkflowConnection> GetOutputConnections(string nodeId)
    {
        var connections = new List<WorkflowConnection>();
        foreach (var conn in Workflow.Connections)
        {
            if (conn.SourceNodeId == nodeId)
                connections.Add(conn);
        }
        return connections;
    }

    /// <summary>
    /// 验证执行上下文
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (Workflow == null)
        {
            errors.Add("工作流为空");
            return (false, errors);
        }

        // 验证所有启用的节点都有参数
        foreach (var node in GetEnabledNodes())
        {
            if (node.Parameters == null)
            {
                errors.Add($"节点 {node.Name} ({node.Id}) 没有参数配置");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 获取执行统计信息
    /// </summary>
    public string GetStatistics()
    {
        var enabledNodes = GetEnabledNodes();
        var globalVarsCount = _globalVariables.Count;

        return $"节点数: {enabledNodes.Count}/{Workflow.Nodes.Count}, " +
               $"连接数: {Workflow.Connections.Count}, " +
               $"全局变量: {globalVarsCount}";
    }

    /// <summary>
    /// 从工作流和全局变量创建执行上下文
    /// </summary>
    public static RunContext Create(Workflow workflow, List<GlobalVariable> globalVariables)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        return new RunContext(globalVariables ?? new List<GlobalVariable>())
        {
            Workflow = workflow
        };
    }
}
