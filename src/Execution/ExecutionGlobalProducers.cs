using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Resolves a completed producer's frozen outputs and publishes its pure variable batch.</summary>
internal static class ExecutionGlobalProducers
{
    internal static void Complete(ExecutionRunRequest request, string completedNode, Guid rootRunId, Guid scopeId,
        CancellationToken cancellation, IReadOnlyDictionary<BindingNodeId, RunIdentity> expected,
        IReadOnlyDictionary<string, DeclaredOutputSnapshot> results)
    {
        var operations = request.GlobalProducers.Where(operation => operation.Source.ScopeId == request.Workflow.WorkflowId
            && operation.Source.NodeId == completedNode).ToArray();
        if (operations.Length == 0) return;
        var values = request.VariableValues ?? throw new InvalidOperationException("全局生产缺少本批变量槽。");
        var workflow = request.Workflow;
        if (workflow.DocumentRevision < 0) throw new InvalidOperationException("全局生产要求有效的方案版本。 ");
        var sources = workflow.Nodes.Select(item => new BindingSource(new(workflow.WorkflowId, item.Node.NodeId), scopeId,
            new ToolSchema(Array.Empty<Port>(), item.Node.Schema.Outputs, outputDisplay: item.Node.Schema.OutputDisplay), 0)).ToArray();
        var produced = new Dictionary<string, (object? Value, Diagnostic? Issue)>(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            cancellation.ThrowIfCancellationRequested();
            if (workflow.Nodes.Any(item => item.Node.NodeId == operation.OperationId))
                throw new InvalidOperationException("全局生产身份与节点身份冲突。 ");
            var identity = workflow.Owner.Begin(ExecutionNodeSnapshot.MakeOwnerDomainId(workflow.WorkflowId, operation.OperationId),
                scopeId, FoundationContract.Major + "." + FoundationContract.Minor, cancellation, rootRunId: rootRunId);
            try
            {
                using var executing = workflow.Owner.EnterRun(identity);
                var consumer = new BindingNodeId(workflow.WorkflowId, operation.OperationId);
                var compilation = new BindingScopeSnapshot(consumer, workflow.Owner.Id, workflow.DocumentRevision,
                    sources, sources.Select(source => source.Node), Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>());
                var binding = new ParameterBindingService().CompileBinding(operation.Source, operation.Target, compilation);
                if (binding.Plan is null)
                { produced.Add(operation.VariableId, (null, binding.Diagnostic ?? new Diagnostic("global-binding", "生产声明不可用。"))); continue; }
                var sourceId = new BindingNodeId(workflow.WorkflowId, operation.Source.NodeId);
                var candidates = new List<BindingNodeResult>();
                using var reader = results.TryGetValue(operation.Source.NodeId, out var result) ? result.AcquireReader() : null;
                if (reader?.Values is { } resultValues)
                    candidates.Add(new(sourceId, reader.Snapshot.Identity, reader.Snapshot.SchemaRevision,
                        reader.Snapshot.Schema.Fingerprint, reader.Snapshot.IsSuccess, resultValues));
                var snapshot = new BindingRunSnapshot(consumer, identity.ToInvocationIdentity(operation.OperationId),
                    workflow.DocumentRevision, candidates, expected);
                var resolved = new ParameterBindingService().ResolveBinding(binding.Plan, snapshot);
                if (!resolved.Success)
                { produced.Add(operation.VariableId, (null, resolved.Diagnostic ?? new Diagnostic("global-value", "生产值不可用。"))); continue; }
                var value = Materialize(operation.Target, operation.ValueType, resolved.Value);
                cancellation.ThrowIfCancellationRequested();
                produced.Add(operation.VariableId, (value, null));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            { produced.Add(operation.VariableId, (null, new Diagnostic("global-production", error.Message))); }
            finally { identity.CompleteCallbacks(); }
        }
        values.Publish(produced, cancellation);
    }

    internal static object? Materialize(Port target, Type targetType, object? value)
    {
        var canonical = ValueRules.Freeze(target, value);
        if (canonical is null) return null;
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type.IsArray && type.GetArrayRank() == 1)
        {
            IEnumerable<object?> items = canonical switch
            { SequenceValue sequence => sequence, Array array when array.Rank == 1 => array.Cast<object?>(), _ => throw new ArgumentException("需要标准序列。") };
            var converted = items.Select(item => MaterializeScalar(item!, type.GetElementType()!)).ToArray();
            var result = Array.CreateInstance(type.GetElementType()!, converted.Length);
            for (var index = 0; index < converted.Length; index++) result.SetValue(converted[index], index);
            return result;
        }
        return MaterializeScalar(canonical, type);
    }
    private static object MaterializeScalar(object value, Type type)
    {
        NumericStorage? storage = type == typeof(sbyte) ? NumericStorage.Int8 : type == typeof(byte) ? NumericStorage.UInt8
            : type == typeof(short) ? NumericStorage.Int16 : type == typeof(ushort) ? NumericStorage.UInt16
            : type == typeof(int) ? NumericStorage.Int32 : type == typeof(uint) ? NumericStorage.UInt32
            : type == typeof(long) ? NumericStorage.Int64 : type == typeof(float) ? NumericStorage.Float32
            : type == typeof(double) ? NumericStorage.Float64 : null;
        if (storage is { } number) NumericStorageRules.EnsureFits(value, number);
        checked
        {
            if (type == typeof(sbyte)) return (sbyte)(long)value; if (type == typeof(byte)) return (byte)(long)value;
            if (type == typeof(short)) return (short)(long)value; if (type == typeof(ushort)) return (ushort)(long)value;
            if (type == typeof(int)) return (int)(long)value; if (type == typeof(uint)) return (uint)(long)value;
            if (type == typeof(long)) return (long)value; if (type == typeof(float)) return (float)(double)value;
            if (type == typeof(double)) return (double)value;
        }
        if (type == typeof(string) && value is string || type == typeof(bool) && value is bool) return value;
        throw new ArgumentException("全局变量值类型不受当前纯值合同支持。 ");
    }
}
