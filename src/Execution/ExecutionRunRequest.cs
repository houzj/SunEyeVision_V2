using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Execution;

/// <summary>A single-use invocation request. Captured input preparers transfer only when claimed.</summary>
public sealed class ExecutionRunRequest : IDisposable
{
    private int _state;
    internal ExecutionRunRequest(ExecutionWorkflowSnapshot workflow,
        IReadOnlyDictionary<string, IHostInputPreparer> hostInputs,
        RuntimeParameterValueAcceptance? parameterValues, ExecutionVariableValues? variableValues,
        IReadOnlyList<ExecutionGlobalProducerOperation> globalProducers,
        IReadOnlyImage? workflowImage, IDisposable? workflowImageLease)
    {
        Workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        ArgumentNullException.ThrowIfNull(hostInputs);
        var nodeIds = workflow.Nodes.Select(item => item.Node.NodeId).ToHashSet(StringComparer.Ordinal);
        var inputs = new Dictionary<string, IHostInputPreparer>(StringComparer.Ordinal);
        foreach (var (nodeId, preparer) in hostInputs)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || preparer is null || !nodeIds.Contains(nodeId))
                throw new ArgumentException("宿主输入必须属于本次执行图中的真实节点。", nameof(hostInputs));
            inputs.Add(nodeId, preparer);
        }
        HostInputs = new ReadOnlyDictionary<string, IHostInputPreparer>(inputs);
        ParameterValues = parameterValues;
        VariableValues = variableValues;
        ArgumentNullException.ThrowIfNull(globalProducers);
        var producers = globalProducers.ToArray();
        if (producers.Any(item => item is null) ||
            producers.Select(item => item.VariableId).Distinct(StringComparer.Ordinal).Count() != producers.Length ||
            producers.Select(item => item.VariableName).Distinct(StringComparer.Ordinal).Count() != producers.Length ||
            (producers.Length > 0 && variableValues is null) ||
            (variableValues is not null && producers.Any(item => !variableValues.Matches(item.VariableId, item.VariableName))))
            throw new ArgumentException("全局生产操作必须有唯一变量身份及本批变量槽。", nameof(globalProducers));
        GlobalProducers = Array.AsReadOnly(producers);
        if ((workflowImage is null) != (workflowImageLease is null))
            throw new ArgumentException("流程输入图像必须与其本轮持有租约一起提供。", nameof(workflowImage));
        WorkflowImage = workflowImage;
        WorkflowImageLease = workflowImageLease;
    }
    public ExecutionWorkflowSnapshot Workflow { get; }
    internal IReadOnlyDictionary<string, IHostInputPreparer> HostInputs { get; }
    internal RuntimeParameterValueAcceptance? ParameterValues { get; }
    // The root run owns this batch across its workflows; the request borrows it.
    internal ExecutionVariableValues? VariableValues { get; }
    internal IReadOnlyList<ExecutionGlobalProducerOperation> GlobalProducers { get; }
    internal IReadOnlyImage? WorkflowImage { get; }
    private IDisposable? WorkflowImageLease { get; }

    internal void Claim()
    {
        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            throw new InvalidOperationException("工作流运行请求已经执行或释放，不能重复接纳。");
    }

    public void Dispose()
    {
        var previous = Interlocked.CompareExchange(ref _state, 2, 0);
        if (previous == 2) return;
        if (previous == 1) throw new InvalidOperationException("运行请求已交给执行器，必须等待运行结束后释放。");
        ReleaseInputs();
    }

    internal void Complete()
    {
        if (Interlocked.CompareExchange(ref _state, 2, 1) != 1)
            throw new InvalidOperationException("工作流运行请求没有有效的执行所有权。");
        ReleaseInputs();
    }

    private void ReleaseInputs()
    {
        var failures = new List<Exception>();
        foreach (var preparer in new HashSet<IHostInputPreparer>(HostInputs.Values, ReferenceEqualityComparer.Instance))
            try { preparer.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (WorkflowImageLease is not null && !HostInputs.Values.Any(input => ReferenceEquals(input, WorkflowImageLease)))
            try { WorkflowImageLease.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0) throw new AggregateException("宿主输入释放失败。", failures);
    }
}
