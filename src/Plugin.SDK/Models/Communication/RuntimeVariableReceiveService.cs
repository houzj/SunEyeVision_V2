using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

/// <summary>Producer values belong to the running solution, never to persisted design initial values.</summary>
public sealed class RuntimeVariableReceiveService(ICommunicationReceiveService inner, IDictionary<string, object?>? runtimeValues,
    IDictionary<string, SunEyeVision.Plugin.SDK.Execution.Results.ToolResults>? nodeResults = null, string? workflowId = null) : ICommunicationReceiveService
{
    public string? WorkflowId { get; } = workflowId;
    public IReadOnlyList<CommunicationReceiveSource> GetSources() => inner.GetSources();
    public IReadOnlyList<ReceiveVariableSource> GetVariables() => inner.GetVariables();
    public Task<CommunicationReceiveSample> ReadAsync(string deviceId, string targetId, CancellationToken token = default) => inner.ReadAsync(deviceId, targetId, token);
    public IReadOnlyDictionary<string, ReceiveVariableValue> ReadVariables(IReadOnlyList<string> ids)
    {
        var catalog = GetVariables().ToDictionary(v => v.Id);
        var current = new Dictionary<string, ReceiveVariableValue>();
        var ordinary = ids.Distinct().Where(id => !catalog.TryGetValue(id, out var v) || !v.HasProducer).ToArray();
        if (ordinary.Length > 0)
            foreach (var pair in inner.ReadVariables(ordinary)) current.Add(pair.Key, pair.Value);
        lock ((object?)runtimeValues ?? current)
            foreach (var id in ids.Distinct().Where(id => catalog.TryGetValue(id, out var v) && v.HasProducer))
            {
                var variable = catalog[id];
                object? value;
                if (nodeResults != null && (string.IsNullOrEmpty(variable.ProducerWorkflowId) || variable.ProducerWorkflowId == WorkflowId)
                    && nodeResults.TryGetValue(variable.ProducerNodeId, out var produced))
                {
                    if (!produced.IsSuccess) throw new InvalidOperationException("变量生产者执行失败。");
                    value = SunEyeVision.Plugin.SDK.Execution.Parameters.OutputVariableResolver.Resolve(produced, variable.ProducerField);
                }
                else if (runtimeValues != null)
                {
                    if (!runtimeValues.TryGetValue(variable.Name, out value)) throw new InvalidOperationException("本次运行没有所选变量的生产者结果。");
                }
                else value = inner.ReadVariables(new[] { id })[id].Value;
                if (value == null) throw new InvalidOperationException("变量生产者没有有效输出。");
                value = CommunicationReceiveService.Normalize(value);
                if (CommunicationReceiveService.ValueType(value) != variable.DataType) throw new InvalidOperationException("生产者结果类型不匹配。");
                current.Add(id, new(variable, value));
            }
        return current;
    }
}
