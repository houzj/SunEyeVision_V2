using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow;

/// <summary>
/// 工作流定义的唯一修改入口。
/// UI 可以展示定义，但节点、连接的增删必须通过此服务完成，避免产生第二份工作流状态。
/// </summary>
public sealed class WorkflowDefinitionService
{
    private readonly WorkflowEngine _engine;

    public WorkflowDefinitionService(WorkflowEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public Workflow GetRequired(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));

        return _engine.GetWorkflowById(workflowId)
            ?? throw new InvalidOperationException($"工作流定义未注册: {workflowId}");
    }

    public Workflow Register(Workflow workflow)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        var existing = _engine.GetWorkflowById(workflow.Id);
        if (existing != null && !ReferenceEquals(existing, workflow))
            throw new InvalidOperationException(
                $"工作流 ID 已注册到另一份定义: {workflow.Id}。禁止创建重复事实源。");

        if (existing == null)
            _engine.RegisterWorkflow(workflow);

        return existing ?? workflow;
    }

    public Workflow GetOrCreate(string workflowId, string name)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
            throw new ArgumentException("工作流 ID 不能为空。", nameof(workflowId));

        var existing = _engine.GetWorkflowById(workflowId);
        if (existing != null)
            return existing;

        return Register(new Workflow(workflowId, string.IsNullOrWhiteSpace(name) ? "新建工作流" : name));
    }

    public void AttachToSolution(Solution solution, Workflow workflow)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));

        var index = solution.Workflows.FindIndex(item =>
            string.Equals(item.Id, workflow.Id, StringComparison.Ordinal));
        if (index < 0)
            solution.Workflows.Add(workflow);
        else if (!ReferenceEquals(solution.Workflows[index], workflow))
            solution.Workflows[index] = workflow;
    }

    public bool DetachFromSolution(Solution solution, string workflowId)
    {
        if (solution == null) throw new ArgumentNullException(nameof(solution));
        var workflow = solution.Workflows.FirstOrDefault(item =>
            string.Equals(item.Id, workflowId, StringComparison.Ordinal));
        return workflow != null && solution.Workflows.Remove(workflow);
    }

    public void AddNode(string workflowId, WorkflowNodeBase node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        var workflow = GetRequired(workflowId);
        if (workflow.Nodes.Any(item => item.Id == node.Id))
            throw new InvalidOperationException($"工作流中已存在节点: {node.Id}");

        workflow.AddNode(node);
    }

    public bool RemoveNode(string workflowId, string nodeId) =>
        GetRequired(workflowId).RemoveNode(nodeId);

    public void AddConnection(string workflowId, WorkflowConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        var workflow = GetRequired(workflowId);
        workflow.AddConnection(connection);
    }

    public bool RemoveConnection(string workflowId, string connectionId)
    {
        var workflow = GetRequired(workflowId);
        var connection = workflow.Connections.FirstOrDefault(item => item.Id == connectionId);
        return connection != null && workflow.Connections.Remove(connection);
    }

    public IReadOnlyList<string> Validate(string workflowId)
    {
        var workflow = GetRequired(workflowId);
        return WorkflowDefinitionValidator.Validate(workflow, _engine.GetWorkflowById);
    }
}

/// <summary>
/// 执行快照编译前的工作流定义校验器。
/// 校验只读定义，不修改 UI、引擎或方案对象。
/// </summary>
public static class WorkflowDefinitionValidator
{
    public static IReadOnlyList<string> Validate(
        Workflow workflow,
        Func<string, Workflow?>? workflowResolver = null)
        => Validate(workflow, workflowResolver, validateBindings: true);

    public static IReadOnlyList<string> Validate(
        Workflow workflow, Func<string, Workflow?>? workflowResolver, bool validateBindings)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        var errors = new List<string>();
        var ids = workflow.Nodes.Select(node => node.Id).ToList();

        if (ids.Any(string.IsNullOrWhiteSpace))
            errors.Add("存在空节点 ID。");
        if (ids.Count != ids.Distinct(StringComparer.Ordinal).Count())
            errors.Add("存在重复节点 ID。");

        var known = new HashSet<string>(ids, StringComparer.Ordinal);
        foreach (var connection in workflow.Connections)
        {
            if (!known.Contains(connection.SourceNodeId) || !known.Contains(connection.TargetNodeId))
            {
                errors.Add($"连接 {connection.SourceNodeId} → {connection.TargetNodeId} 引用了不存在的节点。");
            }
        }

        foreach (var node in workflow.Nodes)
        {
            foreach (var setting in validateBindings
                ? node.ParamSettings?.GetDynamicSettings() ?? Enumerable.Empty<SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting>()
                : Enumerable.Empty<SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting>())
            {
                if (string.IsNullOrWhiteSpace(setting.SourceNodeId) ||
                    string.IsNullOrWhiteSpace(setting.SourceProperty))
                {
                    errors.Add($"节点 {node.Name} 的参数 {setting.ParameterName} 绑定定义不完整。");
                    continue;
                }

                if (setting.SourceNodeId != "__GlobalVars__" &&
                    setting.SourceNodeId != "__SubroutineInputs__" &&
                    !known.Contains(setting.SourceNodeId))
                {
                    errors.Add($"节点 {node.Name} 的参数 {setting.ParameterName} 引用了不存在的源节点 {setting.SourceNodeId}。");
                }
            }

            if (!string.IsNullOrWhiteSpace(node.SubroutineId))
            {
                if (string.Equals(node.SubroutineId, workflow.Id, StringComparison.Ordinal))
                    errors.Add($"节点 {node.Name} 不能引用自身工作流。");
                else if (workflowResolver != null && workflowResolver(node.SubroutineId) == null)
                    errors.Add($"节点 {node.Name} 引用了不存在的子程序工作流 {node.SubroutineId}。");
            }
        }

        if (errors.Count == 0 && workflow.DetectCycles().Count > 0)
            errors.Add("工作流连接存在循环依赖。");

        if (workflowResolver != null)
            ValidateSubroutineCalls(workflow, workflowResolver, errors);

        return errors;
    }

    private static void ValidateSubroutineCalls(
        Workflow root, Func<string, Workflow?> resolve, List<string> errors)
    {
        // 按当前路径判环，已完成的共享子图可以合法复用；使用显式栈避免深层定义耗尽调用栈。
        var completed = new HashSet<string>(StringComparer.Ordinal);
        var active = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();
        var pending = new Stack<(Workflow Flow, bool Exit)>();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            var (flow, exit) = pending.Pop();
            if (exit)
            {
                active.Remove(flow.Id);
                path.RemoveAt(path.Count - 1);
                completed.Add(flow.Id);
                continue;
            }
            if (active.TryGetValue(flow.Id, out var start))
            {
                errors.Add($"子程序调用存在循环依赖：{string.Join(" → ", path.Skip(start).Append(flow.Id))}。");
                continue;
            }
            if (completed.Contains(flow.Id)) continue;
            active.Add(flow.Id, path.Count);
            path.Add(flow.Id);
            pending.Push((flow, true));
            foreach (var id in flow.Nodes.Select(n => n.SubroutineId)
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Reverse())
            {
                var child = string.Equals(id, root.Id, StringComparison.Ordinal) ? root : resolve(id!);
                if (child != null) pending.Push((child, false));
                else if (!ReferenceEquals(flow, root))
                    errors.Add($"工作流 {flow.Id} 引用了不存在的子程序工作流 {id}。");
            }
        }
    }
}
