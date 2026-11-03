using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>The resource-owning invocation shared by the formal and debug execution paths.</summary>
internal static class ExecutionToolInvocation
{
    internal static async ValueTask<DeclaredOutputSnapshot> ExecuteAsync(ExecutionWorkflowSnapshot workflow,
        ExecutionNodeSnapshot node, ExecutionBindingPlan.NodePlan plan, ToolExecutionIdentity identity,
        IReadOnlyDictionary<BindingNodeId, RunIdentity> expected,
        IReadOnlyDictionary<string, DeclaredOutputSnapshot> results, ExecutionVariableValues? variables,
        RuntimeParameterValueAcceptance? parameterValues, IHostInputPreparer? hostInput,
        IReadOnlyImage? workflowImage, Action<string> log,
        IReadOnlyDictionary<string, DeclaredOutputField>? resultFields)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(log);
        var invocationIdentity = identity.ToInvocationIdentity(node.NodeId);
        var resources = new FrozenOutputResources();
        IDisposable? admission = null;
        IReadOnlyList<IDisposable> sourceReaders = Array.Empty<IDisposable>();
        RuntimeGenerationLease? generation = null;
        PreparedHostInvocation? preparedHost = null;
        ToolOutcome? outcome = null;
        DeclaredOutputSnapshot? result = null;
        var transferred = false;
        Exception? failure = null;
        var timer = Stopwatch.StartNew();
        try
        {
            if (!ReferenceEquals(workflow.Owner, node.Owner) || !ReferenceEquals(workflow.Directory, node.Directory)
                || !workflow.Nodes.Any(item => ReferenceEquals(item.Node, node))
                || !ReferenceEquals(identity.Owner, node.Owner) || identity.NodeId != node.OwnerDomainId
                || invocationIdentity.DocumentSessionId != node.DocumentOwnerId)
                throw new InvalidOperationException("执行身份不属于冻结方案中的实际节点。");
            admission = node.Owner.EnterRun(identity);
            identity.CancellationToken.ThrowIfCancellationRequested();
            generation = node.Directory.AcquireInvocation();
            var resolved = ExecutionBindingInputResolver.Resolve(workflow, node, plan, identity, expected, results, variables, parameterValues);
            sourceReaders = resolved.Readers;
            var settings = node.MaterializeSettings(resolved.Settings);
            var values = new Dictionary<string, object?>(resolved.Inputs, StringComparer.Ordinal);
            var context = new InvocationContext(invocationIdentity, identity.CancellationToken, resources, log);
            IToolExecutionContext bound = context;
            if (node.Directory.GetHostInputs(node.Configuration.ToolId) is not null)
            {
                preparedHost = await HostInputCoordinator.PrepareAsync(node,
                    hostInput ?? throw new InvalidOperationException("工具缺少本轮宿主输入。"),
                    settings, identity, node.NodeId, context).ConfigureAwait(false);
                foreach (var value in preparedHost.Values)
                    if (!values.TryAdd(value.Key, value.Value)) throw new InvalidOperationException("宿主输入与持久绑定冲突：" + value.Key);
                bound = preparedHost.Context;
            }
            else if (hostInput is not null) throw new InvalidOperationException("工具没有声明本轮提供的宿主输入。 ");
            if (node.Schema.WorkflowImageInputId is { } imagePort && !values.ContainsKey(imagePort) && workflowImage is not null)
                values.Add(imagePort, workflowImage);
            var inputs = new InputValues(node.Schema.Inputs, values);
            var requests = plan.Requests.Concat(node.RequiredDisplayOutputs).ToArray();
            outcome = await ToolRuntimeExecutor.ExecuteAsync(node.Definition, settings, inputs, bound, requests).ConfigureAwait(false);
            var owned = new GenerationResourceOwner(resources, generation);
            generation = null;
            // Capture consumes both output and owned resources even if it throws.
            transferred = true;
            result = DeclaredOutputSnapshot.Capture(node.Schema, 0, identity, node.NodeId,
                outcome, owned, timer.Elapsed.TotalMilliseconds, fields: resultFields, sourceWorkflowId: node.WorkflowId);
            return result;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            var cleanup = new List<Exception>();
            if (!transferred)
            {
                try { (outcome?.Output as IDisposable)?.Dispose(); } catch (Exception error) { cleanup.Add(error); }
                if (!ReferenceEquals(outcome?.Output, resources))
                    try { resources.Dispose(); } catch (Exception error) { cleanup.Add(error); }
                try { generation?.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            }
            foreach (var reader in sourceReaders) try { reader.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            if (preparedHost is not null)
                try { await preparedHost.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { cleanup.Add(error); }
            try { admission?.Dispose(); } catch (Exception error) { cleanup.Add(error); }
            if (cleanup.Count > 0)
            {
                if (result is not null) try { result.Dispose(); } catch (Exception error) { cleanup.Add(error); }
                if (failure is not null) cleanup.Insert(0, failure);
                throw new AggregateException("Runtime invocation resource cleanup failed.", cleanup);
            }
        }
    }

    private sealed class InvocationContext(RunIdentity identity, CancellationToken cancellation,
        IOutputResources resources, Action<string> log) : IToolExecutionContext
    {
        public RunIdentity Identity => identity;
        public CancellationToken Cancellation => cancellation;
        public IOutputResources Resources => resources;
        public void Log(string message) => log(message);
    }
}
