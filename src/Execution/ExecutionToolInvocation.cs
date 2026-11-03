using System.Diagnostics;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>The resource-owning invocation shared by the formal and debug execution paths.</summary>
internal static class ExecutionToolInvocation
{
    internal static async ValueTask<DeclaredOutputSnapshot> ExecuteAsync(ExecutionNodeSnapshot node,
        ToolExecutionIdentity identity, IReadOnlyDictionary<string, object?> inputValues,
        IReadOnlyDictionary<string, object?> settingValues, IEnumerable<OutputValueRequest> requests,
        IHostInputPreparer? hostInput, IReadOnlyList<IDisposable> sourceReaders, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(sourceReaders); ArgumentNullException.ThrowIfNull(log);
        var invocationIdentity = identity.ToInvocationIdentity(node.NodeId);
        var resources = new FrozenOutputResources();
        IDisposable? admission = null;
        RuntimeGenerationLease? generation = null;
        PreparedHostInvocation? preparedHost = null;
        ToolOutcome? outcome = null;
        DeclaredOutputSnapshot? result = null;
        var transferred = false;
        Exception? failure = null;
        var timer = Stopwatch.StartNew();
        try
        {
            if (invocationIdentity.DocumentSessionId != node.DocumentOwnerId)
                throw new InvalidOperationException("执行身份不属于冻结方案 owner。");
            admission = node.Owner.EnterRun(identity);
            identity.CancellationToken.ThrowIfCancellationRequested();
            generation = node.Directory.AcquireInvocation();
            var settings = node.MaterializeSettings(settingValues);
            var values = new Dictionary<string, object?>(inputValues, StringComparer.Ordinal);
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
            var inputs = new InputValues(node.Schema.Inputs, values);
            outcome = await ToolRuntimeExecutor.ExecuteAsync(node.Definition, settings, inputs, bound, requests).ConfigureAwait(false);
            var owned = new GenerationResourceOwner(resources, generation);
            generation = null;
            // Capture consumes both output and owned resources even if it throws.
            transferred = true;
            result = DeclaredOutputSnapshot.Capture(node.Schema, 0, identity, node.NodeId,
                outcome, owned, timer.Elapsed.TotalMilliseconds, sourceWorkflowId: node.WorkflowId);
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
